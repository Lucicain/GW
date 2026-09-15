using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.BarterSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Field arrest without a war declaration. A Grey Warden who catches up with an
    /// offender in the open states the charge; the offender first judges the odds,
    /// his own temper and the Warden's standing, and either submits at once, attacks
    /// at once, or leaves the matter open to argument. The argument itself is the
    /// stock persuasion sequence, the same one courtship uses, so the player's Charm,
    /// Leadership, Roguery and Trade all lead somewhere.
    /// </summary>
    public sealed partial class GwpFieldArrestBehavior : CampaignBehaviorBase
    {
        private static GwpRuntimeState.CrimeState CrimeState => GwpRuntimeState.Crime;
        private static GwpRuntimeState.PlayerState PlayerState => GwpRuntimeState.Player;

        private readonly List<PersuasionTask> _reservations = new List<PersuasionTask>();
        // 谈崩之后要打的那支队伍。对话还开着时不能开打，先存在这里。
        private MobileParty? _pendingBattleParty;
        private CrimeRecord? _crime;
        private Hero? _offender;
        private MobileParty? _offenderParty;
        private int _fine;
        private readonly GwpFieldDialogueVoice _voice = new GwpFieldDialogueVoice();
        private bool HasReceivedCash => _offender != null && (GwpFieldReportLedger.Instance?.PendingReceivedFor(_offender.StringId) ?? 0) > 0;
        private GwpOffenderDesire _desire;
        private bool _enforcementAccepted;
        private bool _paymentAccepted;
        private PersuasionTask? _lastArgumentTask;
        private PersuasionOptionArgs? _pendingArgument;
        private PersuasionOptionArgs? _lastArgument;
        private PersuasionOptionResult _lastResult;
        private bool _refusesToTalk;
        private GwpAssetPayment? _collection;
        private bool _collectionWasBroke;
        private bool _collectionEmpty;
        private bool _collectionFull;

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CheckGraceDeadline);
            CampaignEvents.ConversationEnded.AddNonSerializedListener(this, OnConversationEnded);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, OnArgumentCommitted);
        }

        /// <summary>
        /// 翻脸动手不能在对话的 consequence 里直接拉战斗：那时对话任务还在跑，
        /// PlayerEncounter.StartBattle() 起不来，玩家会被原样弹回遭遇界面，再点
        /// 一次又走一遍流程（日志里一次执法出现两条 ATTACK 就是这么来的）。
        /// 先记下要打谁，等对话真正关掉再开打。
        /// </summary>
        private void OnConversationEnded(IEnumerable<CharacterObject> characters)
        {
            _ = characters;
            MobileParty? target = _pendingBattleParty;
            if (target == null) return;

            _pendingBattleParty = null;
            GwpFieldArrestHostility.StartBattleNow(target);
        }

        // Negotiation is encounter-local. Legacy persisted attempt locks are retired.
        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("gwp_duel_payment_hero", ref _duelPaymentHero);
            dataStore.SyncData("gwp_grace_hero", ref _graceHeroId);
            dataStore.SyncData("gwp_grace_due", ref _graceDueHours);
            dataStore.SyncData("gwp_grace_notice", ref _graceNoticeShown);
        }

        #region 对话注册

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
#if GWP_DIAGNOSTICS
            RegisterDesireTestDialogues(starter);
#endif
            _pendingDuelOffenderId = string.Empty;
            _pendingDuelFine = 0;
            // ── 入口 ────────────────────────────────────────────────────────────
            starter.AddPlayerLine(
                "gwp_fa_open", "hero_main_options", "gwp_fa_greeting",
                GwpText.Get("{=gwp_fieldarrest_open}Stand where you are. I speak for the Grey Wardens."),
                FieldArrestAvailableCondition, PrepareFieldArrest, 110);

#if GWP_DIAGNOSTICS
            // [GWP_TEST_SCAFFOLD] 手工造案的对话入口，定稿后整段删除。
            // 调试用：新档的案件池是空的，等 AI 自己去抢一票要很久。这一条当场
            // 给对面记一笔村庄暴力，连带人命，好把执法流程立刻跑起来。
            starter.AddPlayerLine(
                "gwp_fa_debug_seed", "hero_main_options", "gwp_fa_debug_seed_reply",
                GwpText.Get("{=gwp_fa_debug_seed}[Debug] Enter a village-violence case against this lord."),
                DebugSeedAvailableCondition, null, 108);

            starter.AddDialogLine(
                "gwp_fa_debug_seed_reply", "gwp_fa_debug_seed_reply", "close_window",
                GwpText.Get("{=gwp_fa_debug_seed_reply}[Debug] The case is on the books."),
                null, DebugSeedConsequence);
#endif

            starter.AddDialogLine(
                "gwp_fa_greeting", "gwp_fa_greeting", "gwp_fa_charge_options",
                "{" + GwpTextKeys.FieldArrestOpening + "}", OpeningCondition, null);

            starter.AddPlayerLine(
                "gwp_fa_charge", "gwp_fa_charge_options", "gwp_fa_layer1_open",
                "{" + GwpTextKeys.FieldArrestCharge + "}", ChargeCondition, null);

            starter.AddPlayerLine("gwp_duel_return_collect", "gwp_fa_charge_options", "gwp_fa_settled",
                GwpText.Get("{=gwp_duel_inspect_property}Show me your property. We will settle the fine now."),
                () => _offender != null && _duelPaymentHero == _offender.StringId && IsAssignedCase(_offender), PrepareDuelPropertyCollection);
            starter.AddPlayerLine("gwp_grace_collect", "gwp_fa_charge_options", "gwp_fa_settled",
                GwpText.Get("{=gwp_grace_collect}Show me what you can pay now."), GraceCollectionAvailable, PrepareGraceCollection);

            starter.AddPlayerLine(
                "gwp_fa_walk_away", "gwp_fa_charge_options", "gwp_fa_dismissed",
                GwpText.Get("{=gwp_fieldarrest_walk_away}Never mind. Go on your way."), null, null);

            starter.AddDialogLine(
                "gwp_fa_dismissed", "gwp_fa_dismissed", "close_window",
                GwpText.Get("{=gwp_fieldarrest_dismissed}Then stop wasting my daylight."),
                null, DismissConsequence);

            // ── 第一层：他认不认这次执法 ────────────────────────────────────────
            starter.AddDialogLine("gwp_fa_refuse", "gwp_fa_layer1_open", "gwp_fa_layer1_lost_options",
                "{GWP_VOICE_REFUSE}", () => (_refusesToTalk) && SetVoice("refuse", "{=gwp_fa_refuse_strength}I will not accept this demand. We have nothing to discuss."), EndPersuasionConsequence, 110);
            starter.AddDialogLine(
                "gwp_fa_layer1_open", "gwp_fa_layer1_open", "gwp_fa_layer1_next",
                "{GWP_VOICE_HEAR}", () => (!_refusesToTalk) && SetVoice("hear", "{=gwp_fa_hear_charge}I have heard the charge. First tell me why I should answer to you."), BeginLayerOneConsequence);

            starter.AddDialogLine(
                "gwp_fa_layer1_failed", "gwp_fa_layer1_next", "gwp_fa_layer1_lost",
                "{GWP_VOICE_LOST1}", () => (LayerFailedCondition()) && SetVoice("lost1", "{=gwp_fa_l1_failed}Enough. Your order has no claim on me, and neither do you."), null);

            starter.AddDialogLine(
                "gwp_fa_layer1_done", "gwp_fa_layer1_next", "gwp_fa_terms_request",
                "{GWP_VOICE_DONE1}", () => (LayerSatisfiedCondition()) && SetVoice("done1", "{=gwp_fa_l1_done}...Very well. I answer to the charge. The question is how."), EndLayerOneConsequence);

            starter.AddDialogLine(
                "gwp_fa_layer1_reservation", "gwp_fa_layer1_next", "gwp_fa_layer1_argument",
                "{=!}{GWP_ARREST_RESERVATION}", UnmetReservationCondition, null);

            RegisterArgumentLines(starter, "gwp_fa_layer1_argument", "gwp_fa_layer1_reaction");

            starter.AddDialogLine("gwp_fa_layer1_retry", "gwp_fa_layer1_reaction", "gwp_fa_layer1_argument",
                "{=!}{PERSUASION_REACTION}", RetryArgumentReactionCondition, null, 110);
            starter.AddDialogLine(
                "gwp_fa_layer1_reaction", "gwp_fa_layer1_reaction", "gwp_fa_layer1_next",
                "{=!}{PERSUASION_REACTION}", ArgumentReactionCondition, null);

            // 兜底：说服状态若因任何原因不再活动，三条判定会同时落空，对话就会
            // 悬在半空并把玩家弹回上一组选项。这一行保证永远有出口。
            starter.AddDialogLine(
                "gwp_fa_layer1_stuck", "gwp_fa_layer1_next", "gwp_fa_layer1_lost",
                "{GWP_VOICE_LOST1}", () => (true) && SetVoice("lost1", "{=gwp_fa_l1_failed}Enough. Your order has no claim on me, and neither do you."), null);

            starter.AddDialogLine(
                "gwp_fa_layer1_lost", "gwp_fa_layer1_lost", "gwp_fa_layer1_lost_options",
                GwpText.Get("{=gwp_fa_l1_lost}I reject this demand from the Wardens. If you insist on force, draw."),
                null, EndPersuasionConsequence);

            // 第一层没过：还想执法就只剩动手。
            starter.AddPlayerLine(
                "gwp_fa_layer1_lost_attack", "gwp_fa_layer1_lost_options", "gwp_fa_attack",
                GwpText.Get("{=gwp_fa_take_by_force}I will enforce the warrant by force."), null, null);

            starter.AddPlayerLine(
                "gwp_fa_layer1_lost_leave", "gwp_fa_layer1_lost_options", "gwp_fa_dismissed",
                GwpText.Get("{=gwp_fa_leave_it}Ride on, then. This is not finished."), null, null);

            starter.AddPlayerLine("gwp_fa_request_terms", "gwp_fa_terms_request", "gwp_fa_layer2_demand",
                GwpText.Get("{=gwp_fa_request_terms}Then tell me how you propose to settle this."), null, null);

            // ── 第二层：他打算怎么兑现 ──────────────────────────────────────────
            starter.AddDialogLine(
                "gwp_fa_layer2_demand", "gwp_fa_layer2_demand", "gwp_fa_offer_options",
                "{" + GwpTextKeys.FieldArrestDemand + "}", DemandCondition, null);
            starter.AddPlayerLine("gwp_fa_offer_accept", "gwp_fa_offer_options", "gwp_fa_desire_done",
                "{" + GwpTextKeys.FieldArrestAccept + "}", AcceptDesireCondition, null);
            starter.AddPlayerLine("gwp_fa_offer_negotiate", "gwp_fa_offer_options", "gwp_fa_payment_begin",
                GwpText.Get("{=gwp_fa_counter_offer}Those terms will not do. Let us discuss the full fine."),
                () => !_paymentAccepted && !GwpOffenderDesires.IsFullPayment(_desire), null);
            starter.AddDialogLine("gwp_fa_payment_begin", "gwp_fa_payment_begin", "gwp_fa_layer2_next",
                "{" + GwpTextKeys.FieldArrestDemand + "}", DemandCondition, BeginLayerTwoConsequence);

            starter.AddDialogLine(
                "gwp_fa_layer2_failed", "gwp_fa_layer2_next", "gwp_fa_layer2_lost",
                "{GWP_VOICE_LOST2}", () => (LayerFailedCondition()) && SetVoice("lost2", "{=gwp_fa_l2_failed}No. I have told you how this will go."), null);

            starter.AddDialogLine(
                "gwp_fa_layer2_done", "gwp_fa_layer2_next", "gwp_fa_layer2_won",
                "{GWP_VOICE_DONE2}", () => (LayerSatisfiedCondition()) && SetVoice("done2", "{=gwp_fa_l2_done}...The full sum, then. Have it your way."), () => _paymentAccepted = _enforcementAccepted);

            starter.AddDialogLine(
                "gwp_fa_layer2_reservation", "gwp_fa_layer2_next", "gwp_fa_layer2_argument",
                "{=!}{GWP_ARREST_RESERVATION}", UnmetReservationCondition, null);

            RegisterArgumentLines(starter, "gwp_fa_layer2_argument", "gwp_fa_layer2_reaction");

            starter.AddDialogLine("gwp_fa_layer2_retry", "gwp_fa_layer2_reaction", "gwp_fa_layer2_argument",
                "{=!}{PERSUASION_REACTION}", RetryArgumentReactionCondition, null, 110);
            starter.AddDialogLine(
                "gwp_fa_layer2_reaction", "gwp_fa_layer2_reaction", "gwp_fa_layer2_next",
                "{=!}{PERSUASION_REACTION}", ArgumentReactionCondition, null);

            starter.AddDialogLine(
                "gwp_fa_layer2_stuck", "gwp_fa_layer2_next", "gwp_fa_layer2_lost",
                "{GWP_VOICE_LOST2}", () => (true) && SetVoice("lost2", "{=gwp_fa_l2_failed}No. I have told you how this will go."), null);

            // 两层都过：按规矩收钱，或者翻脸。
            starter.AddDialogLine(
                "gwp_fa_layer2_won", "gwp_fa_layer2_won", "gwp_fa_layer2_won_options",
                "{" + GwpTextKeys.FieldArrestSubmit + "}", PrepareSubmitCondition, EndPersuasionConsequence);

            starter.AddPlayerLine(
                "gwp_fa_collect", "gwp_fa_layer2_won_options", "gwp_fa_settled",
                "{GWP_CASE_COLLECT_AMOUNT}", PrepareCollectCondition, null);

            starter.AddPlayerLine(
                "gwp_fa_won_attack", "gwp_fa_layer2_won_options", "gwp_fa_attack",
                GwpText.Get("{=gwp_fa_take_anyway}Keep your silver. I am taking you in."), null, null);

            // 第二层没过：照他的办，或者翻脸。
            starter.AddDialogLine(
                "gwp_fa_layer2_lost", "gwp_fa_layer2_lost", "gwp_fa_layer2_lost_options",
                "{" + GwpTextKeys.FieldArrestDemand + "}",
                DemandCondition, EndPersuasionConsequence);

            starter.AddPlayerLine(
                "gwp_fa_accept_desire", "gwp_fa_layer2_lost_options", "gwp_fa_desire_done",
                "{" + GwpTextKeys.FieldArrestAccept + "}", AcceptDesireCondition, null);

            starter.AddPlayerLine(
                "gwp_fa_lost_attack", "gwp_fa_layer2_lost_options", "gwp_fa_attack",
                GwpText.Get("{=gwp_fa_take_by_force}I will enforce the warrant by force."), null, null);

            // ── 三个终局 ────────────────────────────────────────────────────────
            starter.AddDialogLine(
                "gwp_fa_settled", "gwp_fa_settled", "gwp_fa_collection_result",
                GwpText.Get("{=gwp_fa_count_money}Here are the money and goods. Choose what you will take."),
                null, () => OpenCollection(true));

            starter.AddDialogLine(
                "gwp_fa_desire_done", "gwp_fa_desire_done", "close_window",
                GwpText.Get("{=gwp_fa_desire_done_line}Then we understand each other."),
                () => _desire == GwpOffenderDesire.SurrenderSelf || _desire == GwpOffenderDesire.DemandDuel || (_desire == GwpOffenderDesire.RequestGrace && CanOfferGrace), ExecuteDesireConsequence);
            starter.AddDialogLine("gwp_fa_desire_money", "gwp_fa_desire_done", "gwp_fa_collection_result",
                GwpText.Get("{=gwp_fa_count_money}Here are the money and goods. Choose what you will take."),
                () => _desire != GwpOffenderDesire.SurrenderSelf && _desire != GwpOffenderDesire.DemandDuel && _desire != GwpOffenderDesire.RequestGrace,
                () => OpenCollection(false));
            starter.AddDialogLine("gwp_fa_collection_paid", "gwp_fa_collection_result", "close_window",
                GwpText.Get("{=gwp_fa_collection_paid}The money is counted. We are done here."),
                () => _collection?.Applied == true && Campaign.Current.BarterManager.LastBarterIsAccepted, FinishCollection);
            starter.AddDialogLine("gwp_fa_collection_empty", "gwp_fa_collection_result", "gwp_fa_empty_options",
                GwpText.Get("{=gwp_fa_collection_empty}See for yourself. There is no money or property left to give you."), () => _collectionEmpty, null);
            starter.AddPlayerLine("gwp_fa_accept_empty", "gwp_fa_empty_options", "gwp_fa_empty_done",
                GwpText.Get("{=gwp_fa_accept_empty}I will report that you could not pay. Your debt remains."), null, null);
            starter.AddDialogLine("gwp_fa_empty_done", "gwp_fa_empty_done", "close_window",
                GwpText.Get("{=gwp_fa_desire_done_line}Then we understand each other."), null, () => Settle(0, "SETTLED_EMPTY"));
            starter.AddDialogLine("gwp_fa_collection_cancel", "gwp_fa_collection_result", "gwp_fa_offer_options",
                GwpText.Get("{=gwp_fa_collection_cancel}No payment has changed hands. What do you intend to do?"), null,
                () => GwpAiDiagnostics.WriteFieldArrest("COLLECTION_CANCELLED", "offender=" + _offender?.StringId));

            starter.AddDialogLine(
                "gwp_fa_attack", "gwp_fa_attack", "close_window",
                "{GWP_FIELD_FIGHT_REPLY}", PrepareFightReply, AttackConsequence);
            foreach (string token in new[] { "gwp_fa_charge_options", "gwp_fa_terms_request", "gwp_fa_offer_options", "gwp_fa_empty_options",
                "gwp_fa_layer1_argument", "gwp_fa_layer2_argument" })
            {
                starter.AddPlayerLine(token + "_force", token, "gwp_fa_attack",
                    GwpText.Get("{=gwp_fa_take_by_force}I will enforce the warrant by force."), null, null);
                if (token != "gwp_fa_charge_options")
                    starter.AddPlayerLine(token + "_leave", token, "gwp_fa_dismissed",
                        GwpText.Get("{=gwp_fa_leave_it}Ride on, then. This is not finished."), null, null);
            }
            foreach (string token in new[] { "gwp_fa_layer2_lost_options", "gwp_fa_layer2_won_options" })
                starter.AddPlayerLine(token + "_leave", token, "gwp_fa_dismissed",
                    GwpText.Get("{=gwp_fa_leave_it}Ride on, then. This is not finished."), null, null);
        }

        private void RegisterArgumentLines(
            CampaignGameStarter starter, string inputToken, string outputToken)
        {
            for (int index = 0; index < ArgumentsPerReservation; index++)
            {
                int captured = index;
                starter.AddPlayerLine(
                    inputToken + "_" + index,
                    inputToken,
                    outputToken,
                    "{=!}{GWP_ARREST_ARGUMENT_" + index + "}",
                    () => ArgumentCondition(captured),
                    () => ArgumentConsequence(captured),
                    100,
                    null,
                    () => SetupArgument(captured));
            }
        }

        #endregion

        #region 资格与宣告

        private bool FieldArrestAvailableCondition()
        {
            Hero? hero = Hero.OneToOneConversationHero;
            MobileParty? party = MobileParty.ConversationParty;
            if (hero == null || hero == Hero.MainHero || party == null || party == MobileParty.MainParty || !party.IsActive)
                return false;

            // 只在野外执法。城里那条路是另一套（向总督交涉）。
            if (party.CurrentSettlement != null || MobileParty.MainParty?.CurrentSettlement != null)
                return false;


            return Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.HasCommissionAuthority(hero) == true;
        }

        private void PrepareFieldArrest()
        {
            ClearState();
            _voice.BeginEncounter();
            _offender = Hero.OneToOneConversationHero;
            _offenderParty = MobileParty.ConversationParty;
            _crime = CrimeState.GetByOffenderId(_offenderParty?.StringId);
            if (_crime == null || _offender == null) return;
            _fine = CalculateFine(_crime);
            _desire = HasGrace ? GwpOffenderDesire.PayInFull : GwpOffenderDesires.Roll(_offender, _fine,
                CanOfferGrace, _offenderParty?.MemberRoster?.GetTroopRoster().Any(e => !e.Character.IsHero && e.Number > 0) == true);
            float ours = Math.Max(1f, MobileParty.MainParty?.GetTotalLandStrengthWithFollowers() ?? 1f);
            float theirs = Math.Max(1f, _offenderParty?.GetTotalLandStrengthWithFollowers() ?? 1f);
            int resistance = LayerOneResistance();
            double refusalChance = GwpNegotiationPolicy.RefusalChance(resistance,
                _offender.GetTraitLevel(DefaultTraits.Honor), _offender.GetTraitLevel(DefaultTraits.Mercy),
                _offender.GetTraitLevel(DefaultTraits.Generosity), OffenderWealth, _fine);
            _refusesToTalk = !HasGrace && refusalChance >= 0.55d;
            GwpAiDiagnostics.WriteFieldArrest("TALK_WILLINGNESS", "hero=" + _offender.StringId
                + "; ours=" + ours + "; theirs=" + theirs + "; gold=" + _offender.Gold
                + "; resistance=" + resistance + "; refusalChance=" + refusalChance + "; refuses=" + _refusesToTalk);
        }

#if GWP_DIAGNOSTICS
        // [GWP_TEST_SCAFFOLD] 手工造案，测试用，定稿后整段删除。
        private static bool DebugSeedAvailableCondition()
        {
            Hero? hero = Hero.OneToOneConversationHero;
            MobileParty? party = MobileParty.ConversationParty;
            return hero != null && hero != Hero.MainHero
                   && party?.IsActive == true && party != MobileParty.MainParty
                   && party.CurrentSettlement == null
                   && CrimeState.GetByOffenderId(party.StringId)?.HasOpenCase != true;
        }

        /// <summary>
        /// 造一件和真实链路完全一样的案子：走正规的案件登记入口累加案底罚款，
        /// 再按平民人头喂负声望，好让罚金的两段都有真实数值可看。
        /// </summary>
        private static void DebugSeedConsequence()
        {
            Hero? hero = Hero.OneToOneConversationHero;
            MobileParty? party = MobileParty.ConversationParty;
            if (hero == null || party == null) return;

            CrimeState.TryAdd(
                GwpText.Get("{=gwp_policecrimemonitorenhanced_010}Raid Village"),
                party,
                party.GetPosition2D,
                GwpText.Get("{=gwp_fa_debug_victim}[Debug] villagers"));

            const int seededCasualties = 40;
            CrimePool.GetOrCreateHistory(hero).AddCivilianCasualties(seededCasualties);

            CrimeRecord? record = CrimeState.GetByOffenderId(party.StringId);
            if (record?.HasOpenCase == true)
                record.CivilianCasualties += seededCasualties;

            GwpAiDiagnostics.WriteFieldArrest(
                "DEBUG_SEED",
                "offender=" + (hero.StringId ?? "-") +
                "; casualties=" + seededCasualties +
                "; standing=" + CrimePool.GetOrCreateHistory(hero).NegativeStanding);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_fa_debug_seeded}[Debug] {VAR_1} now carries a village-violence case with {VAR_2} dead.",
                    "VAR_1", hero.Name?.ToString() ?? string.Empty,
                    "VAR_2", seededCasualties.ToString()),
                Colors.Cyan));
        }
#endif

        private int AvailableProperty => _offender == null || _offenderParty == null ? 0 : GwpAssetPayment.Wealth(_offender, _offenderParty.Party);
        private int BribeOffer => _offender == null || _offenderParty == null ? 0 : Math.Min(
            GwpAssetPayment.Wealth(_offender, _offenderParty.Party), Math.Max(EnsureFine() * 60 / 100,
                (Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.ExpectedCaseFee ?? 0) * 3 / 2));

        private bool DemandCondition()
        {
            MBTextManager.SetTextVariable("GWP_BRIBE_AMOUNT", BribeOffer);
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestDemand, GwpOffenderDesires.IsFullPayment(_desire) && AvailableProperty < EnsureFine()
                    ? GwpText.Create("{=gwp_case_purse_short}I accept the fine, but my money and goods together are worth only {VAR_1} denars. That is what I can give now.", "VAR_1", AvailableProperty)
                    : _voice.Alternate("demand_" + _desire, GwpOffenderDesires.Line(_desire)));
            MBTextManager.SetTextVariable("GWP_CASE_OFFER_AMOUNT", Math.Min(AvailableProperty,
                EnsureFine() * GwpOffenderDesires.OfferedSharePercent(_desire) / 100));
            return true;
        }

        private bool PrepareSubmitCondition()
        {
            if (_offender == null) return false;
            MBTextManager.SetTextVariable(GwpTextKeys.FieldArrestSubmit,
                AvailableProperty < EnsureFine()
                    ? GwpText.Create("{=gwp_case_purse_short}I accept the fine, but my money and goods together are worth only {VAR_1} denars. That is what I can give now.", "VAR_1", AvailableProperty)
                    : _voice.Line(GwpFieldArrestLines.SubmitKey(GwpFieldArrestLines.Read(_offender))));
            return true;
        }

        private bool PrepareCollectCondition()
        {
            MBTextManager.SetTextVariable("GWP_CASE_COLLECT_AMOUNT", GwpText.Get(
                "{=gwp_case_collect_amount}Present property worth {VAR_1} denars. I will report the payment and any shortfall.",
                "VAR_1", Math.Min(EnsureFine(), AvailableProperty)));
            return true;
        }

        /// <summary>
        /// 玩家的记录在这个人眼里值多少。它只挑他用哪套说辞、以及第一层他肯不肯
        /// 听，不决定任何金额。
        /// </summary>
        internal enum WardenStandingTier
        {
            UnderCharge,
            Ordinary,
            Respected
        }

        internal static WardenStandingTier CurrentStandingTier()
        {
            int standing = PlayerState.Reputation;
            if (standing < 0) return WardenStandingTier.UnderCharge;
            return standing >= GwpTuning.FieldArrest.RespectedStanding
                ? WardenStandingTier.Respected
                : WardenStandingTier.Ordinary;
        }

        private int EnsureFine()
        {
            if (_fine <= 0 && _crime != null)
                _fine = CalculateFine(_crime);
            return _fine;
        }

        /// <summary>
        /// 灰袍是否把这个人的案子交给了玩家。没有委派就没有执法权，哪怕对方
        /// 案底累累，也轮不到一个没接案的猎手上去拦路。
        /// </summary>
        private static bool IsAssignedCase(Hero? offender) =>
            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                ?.IsActiveBountyTarget(offender) == true;

        private bool HasBeenTakenBefore =>
            _offender != null && (CrimePool.GetHistory(_offender)?.TotalArrestCount ?? 0) > 0;

        private bool OpeningCondition()
        {
            if (_offender != null && _duelPaymentHero == _offender.StringId)
            {
                MBTextManager.SetTextVariable(GwpTextKeys.FieldArrestOpening, GwpText.Create("{=gwp_duel_return_collect}You won our duel. I still owe you the payment we agreed."));
                return true;
            }
            if (HasGrace)
            {
                double remaining = Math.Max(0, _graceDueHours - CampaignTime.Now.ToHours);
                MBTextManager.SetTextVariable(GwpTextKeys.FieldArrestOpening, remaining > 0
                    ? GwpText.Create("{=gwp_grace_return_early}You gave me time. There are still {VAR_1} hours left. Have you come for what I have already?", "VAR_1", (int)Math.Ceiling(remaining))
                    : GwpText.Create("{=gwp_grace_return_due}The time we agreed is up. Let us see what I can pay now."));
                return true;
            }
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestOpening,
                HasReceivedCash ? _voice.Line("paid") :
                CurrentStandingTier() == WardenStandingTier.Ordinary && !HasBeenTakenBefore
                    ? _voice.Line("open_" + GwpFieldArrestLines.Read(_offender))
                    : _voice.Question(GwpFieldArrestLines.Opening(CurrentStandingTier(), HasBeenTakenBefore)));
            return true;
        }

        private bool ChargeCondition()
        {
            if (_crime?.HasOpenCase != true || _offender == null || !IsAssignedCase(_offender) || HasGrace || _duelPaymentHero == _offender.StringId)
                return false;

            if (_fine <= 0) return false;


            GwpFieldArrestLines.Temperament temper = GwpFieldArrestLines.Read(_offender);
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestSubmit, _voice.Line(GwpFieldArrestLines.SubmitKey(temper)));
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestResist,
                _voice.Line(GwpFieldArrestLines.ResistKey(temper, CurrentStandingTier())));
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestPlead, _voice.Line(GwpFieldArrestLines.PleadKey(temper)));

            // 整句在这里拼好再交给对话系统。对话文本在注册时就会被解析一次，
            // 那时案情变量还不存在，所以直接把 {GWP_CRIME} 之类写进句子会显示为空白。
            string victim = string.IsNullOrWhiteSpace(_crime.VictimName)
                ? GwpText.Get("{=gwp_fieldarrest_victim_unknown}the Wardens' own record")
                : _crime.VictimName;
            if (victim.StartsWith("【调试】", StringComparison.Ordinal))
                victim = GwpText.Get("{=gwp_field_villagers}local villagers");

            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestCharge,
                GwpText.Get(
                    "{=gwp_fieldarrest_charge}The Grey Wardens charge you with {VAR_1}, against {VAR_2}. The fine is {VAR_3} denars. They sent me to settle this.",
                    "VAR_1", DescribeCrime(_crime.CrimeCategory).ToString(),
                    "VAR_2", victim,
                    "VAR_3", _fine.ToString()));
            return true;
        }

        /// <summary>
        /// 玩家在野外把案子办成了——他收了罚金，或者对方兑现了谈成的处置。这同样是一次
        /// 灰袍执法，必须记进震慑与"最近一次执法"的时间戳，否则罪犯的收敛期不会启动，
        /// 表现就是"我明明罚过他了，他照样接着抢"。押走才算被捕，这里不动拘捕履历。
        /// </summary>
        private static void RegisterFieldEnforcement(Hero? offender, CrimeRecord? crime)
        {
            if (offender == null) return;
            Campaign.Current?.GetCampaignBehavior<PoliceAIDeterrenceBehavior>()
                ?.RegisterPlayerEnforcementSuccess(null, offender,
                    crime?.CrimeCategory ?? GwpCrimeCategory.VillageViolence);
            GwpAiDiagnostics.WriteFieldArrest("FIELD_ENFORCEMENT_REGISTERED",
                "offender=" + (offender.StringId ?? "-") +
                "; category=" + (crime?.CrimeCategory.ToString() ?? "-"));
        }

        private static TextObject DescribeCrime(GwpCrimeCategory category) =>
            category == GwpCrimeCategory.CaravanAttack
                ? GwpText.Create("{=gwp_fieldarrest_crime_caravan}attacking a caravan under Warden protection")
                : GwpText.Create("{=gwp_fieldarrest_crime_village}violence against villagers under Warden protection");

        /// <summary>
        /// Two halves, exactly as the Wardens price the player: the deeds themselves, one
        /// base charge each, and the offender's own negative standing at the standard rate
        /// per point. Nothing here looks at how often he has been arrested - that record
        /// only feeds deterrence, which is what makes a much-arrested lord stop raiding in
        /// the first place.
        /// </summary>
        private static int CalculateBaseFine(CrimeRecord crime) =>
            crime.AccruedBaseFine > 0
                ? crime.AccruedBaseFine
                : GwpFieldArrestPricing.BaseChargeFor(crime.CrimeCategory);

        /// <summary>
        /// 罚金收的是他名下所有没赎回的人命，不是这一件案子里的死亡人数。对外
        /// 报数一律用这个值，否则会出现"说死了 40 个人却按 146 条人命收钱"。
        /// </summary>
        private static int UnredeemedLives(Hero? offender) =>
            offender == null ? 0 : CrimePool.GetHistory(offender)?.UnredeemedLives ?? 0;

        private static int GetNegativeStanding(CrimeRecord crime)
        {
            Hero? offender = crime.OffenderHero;
            return offender == null ? 0 : Math.Max(0, CrimePool.GetHistory(offender)?.NegativeStanding ?? 0);
        }

        private static int CalculateFine(CrimeRecord crime) =>
            CalculateBaseFine(crime)
            + GetNegativeStanding(crime) * GwpTuning.Enforcement.FinePerPoint;

        #endregion

        #region 两层抗性

        /// <summary>
        /// 第一层：他认不认这次执法。三个维度——性格、实力差、玩家在灰袍这边的
        /// 分量。数值越高越难说服；说不动就只剩动手。
        /// </summary>
        private int LayerOneResistance()
        {
            if (_offender == null || _offenderParty == null) return 50;

            float mine = Math.Max(1f, MobileParty.MainParty?.GetTotalLandStrengthWithFollowers() ?? 1f);
            float his = Math.Max(1f, _offenderParty.GetTotalLandStrengthWithFollowers());

            // 打得过你的人不容易低头——但也仅此而已。此前这里直接把
            // his/(his+mine)*100 当成抗性主项，对方稍强就逼近 100，性格（最多
            // ±28）和声望（±12）在它面前完全没有意义，难度恒定 Hard、说辞恒定
            // VeryHard。现在以势均力敌为基线，实力差只作一个有限摆幅，三个维度
            // 才谈得上共同决定。
            float edge = MBMath.ClampFloat(his / (his + mine), 0f, 1f) - 0.5f;
            float odds = GwpTuning.FieldArrest.DecisionBaseline
                         + edge * 2f * GwpTuning.FieldArrest.StrengthSwing;

            int valor = _offender.GetTraitLevel(DefaultTraits.Valor);
            int honor = _offender.GetTraitLevel(DefaultTraits.Honor);
            int mercy = _offender.GetTraitLevel(DefaultTraits.Mercy);
            int calculating = _offender.GetTraitLevel(DefaultTraits.Calculating);

            // 勇武的人硬；讲荣誉、有恻隐、会算计的人肯听。
            float temper = (valor - honor - mercy - calculating)
                           * GwpTuning.FieldArrest.TraitResistStep;

            // 你的分量：有威望的猎手压得住场，戴罪之身反而被顶。
            float badge = CurrentStandingTier() switch
            {
                WardenStandingTier.Respected => -GwpTuning.FieldArrest.StandingSwing,
                WardenStandingTier.UnderCharge => GwpTuning.FieldArrest.StandingSwing,
                _ => 0f
            };

            // 被灰袍抓过的人知道这本账的分量。
            float taught = MBMath.ClampFloat(
                GwpAiDeterrenceState.GetCurrentPenalty(_offender, _crime?.CrimeCategory ?? GwpCrimeCategory.Unknown)
                / Math.Max(1f, GwpTuning.FieldArrest.DeterrenceReference),
                0f, 1.2f) * GwpTuning.FieldArrest.DeterrenceSwing;

            return (int)MBMath.ClampFloat(odds + temper + badge - taught + OffenderStandingPressure() + GwpNegotiationPolicy.PurseResistance(OffenderWealth, EnsureFine()) - _offender.GetTraitLevel(DefaultTraits.Generosity) * 4f, 0f, 100f);
        }

        /// <summary>
        /// 第二层：他咬着自己那套兑现方式不放的力度。特质越极端越难拉回正规全缴，
        /// 罚金越让他肉痛越难。
        /// </summary>
        private float OffenderStandingPressure()
        {
            float fameGap = ((_offender?.Clan?.Renown ?? 0f) - (Hero.MainHero.Clan?.Renown ?? 0f)) / 150f;
            var history = _offender == null ? null : CrimePool.GetHistory(_offender);
            return MBMath.ClampFloat(fameGap, -8f, 8f)
                + Math.Min(8f, (history?.NegativeStanding ?? 0) / 10f)
                - Math.Min(10f, (history?.TotalArrestCount ?? 0) * 2f);
        }

        private int LayerTwoResistance()
        {
            if (_offender == null) return 40;
            float ours = Math.Max(1f, MobileParty.MainParty?.GetTotalLandStrengthWithFollowers() ?? 1f);
            float theirs = Math.Max(1f, _offenderParty?.GetTotalLandStrengthWithFollowers() ?? 1f);
            float force = (theirs / (theirs + ours) - 0.5f) * 16f;
            float rhetoric = MBMath.ClampFloat((_offender.GetSkillValue(DefaultSkills.Charm)
                - Hero.MainHero.GetSkillValue(DefaultSkills.Charm)) / 20f, -10f, 10f);
            float pain = MBMath.ClampFloat(EnsureFine() / (float)Math.Max(1, OffenderWealth), 0f, 2f);
            return (int)MBMath.ClampFloat(30f + GwpOffenderDesires.Insistence(_offender, _desire) * 12f
                + pain * 15f + force + rhetoric + OffenderStandingPressure() * 0.5f, 0f, 100f);
        }

        #endregion

        #region 两轮谈判

        private const int ArgumentsPerReservation = 4;

        private enum NegotiationLayer
        {
            None,
            /// <summary>让他认下这次执法。</summary>
            AcceptEnforcement,
            /// <summary>让他按正规方式全额缴纳。</summary>
            PayByTheBook
        }

        private NegotiationLayer _layer;
        private int _activeResistance;

        // Each encounter starts a fresh attempt with current strength and standing.
        private void BeginLayerOneConsequence()
        {
            BeginLayer(NegotiationLayer.AcceptEnforcement);
        }

        private void BeginLayerTwoConsequence()
        {
            // 他本来就打算照数全缴时不需要第二轮。
            if (GwpOffenderDesires.IsFullPayment(_desire))
            {
                _layer = NegotiationLayer.None;
                return;
            }

            BeginLayer(NegotiationLayer.PayByTheBook);
        }

        private void EndLayerOneConsequence()
        {
            _enforcementAccepted = PersuasionSatisfiedCondition();
            
            if (ConversationManager.GetPersuasionIsActive())
                ConversationManager.EndPersuasion();
            _layer = NegotiationLayer.None;
        }

        internal void EndPersuasionConsequence()
        {
            if (ConversationManager.GetPersuasionIsActive())
                ConversationManager.EndPersuasion();
            _layer = NegotiationLayer.None;
        }

        private void BeginLayer(NegotiationLayer layer)
        {
            if (ConversationManager.GetPersuasionIsActive())
                ConversationManager.EndPersuasion();

            _layer = layer;
            BuildReservations();

            float goal = Math.Max(1, _reservations.Count);
            int resistance = layer == NegotiationLayer.AcceptEnforcement
                ? LayerOneResistance()
                : LayerTwoResistance();

            _activeResistance = resistance;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFieldArrest("NEGOTIATION_CHANCE_CONTEXT", "layer=" + layer + "; resistance=" + resistance
                + "; wealth=" + OffenderWealth + "; fine=" + EnsureFine()
                + "; ours=" + MobileParty.MainParty?.GetTotalLandStrengthWithFollowers()
                + "; theirs=" + _offenderParty?.GetTotalLandStrengthWithFollowers());
#endif
            // 抗性低的人本来就快被说动了，给一点起手进度；抗性高的人从零开始。
            float ease = MBMath.ClampFloat((100 - resistance) / 100f, 0f, 1f);
            float initial = goal * GwpTuning.FieldArrest.InitialProgressShare * ease;


            ConversationManager.StartPersuasion(
                goal, 1f, 1f, 2f, 2f, initial, PersuasionDifficulty.Medium);
        }

        private PersuasionArgumentStrength ResolveArgumentStrength() => PersuasionArgumentStrength.Normal;

        internal void AdjustNativeChances(PersuasionOptionArgs option, ref float success,
            ref float criticalSuccess, ref float criticalFailure, ref float failure)
        {
            if (_layer == NegotiationLayer.None || !_reservations.Any(t => t.Options.Any(o => ReferenceEquals(o, option)))) return;
            float previous = success + criticalSuccess;
            float adjusted = GwpNegotiationPolicy.SuccessChance(previous, _activeResistance,
                _layer == NegotiationLayer.AcceptEnforcement);
            float criticalShare = previous > 0f ? criticalSuccess / previous : (option.GivesCriticalSuccess ? 1f : 0f);
            float failureTotal = failure + criticalFailure;
            float criticalFailureShare = failureTotal > 0f ? criticalFailure / failureTotal : 0f;
            success = adjusted * (1f - criticalShare);
            criticalSuccess = adjusted * criticalShare;
            criticalFailure = (1f - adjusted) * criticalFailureShare;
            failure = Math.Max(0f, 1f - success - criticalSuccess - criticalFailure);
        }

        private int OffenderWealth => _offender == null ? 0 : _offenderParty == null
            ? Math.Max(0, _offender.Gold) : GwpAssetPayment.Wealth(_offender, _offenderParty.Party);

        /// <summary>说辞用光却没到目标，和被当场顶回来一样，都算这一轮谈崩。</summary>
        private bool LayerFailedCondition() =>
            ConversationManager.GetPersuasionIsActive()
            && (ConversationManager.GetPersuasionIsFailure()
                || (!ConversationManager.GetPersuasionProgressSatisfied() && CurrentReservation() == null));

        private bool LayerSatisfiedCondition() =>
            ConversationManager.GetPersuasionIsActive()
            && ConversationManager.GetPersuasionProgressSatisfied();

        private void BuildReservations()
        {
            _reservations.Clear();
            if (_crime == null || _offender == null) return;

            if (_layer == NegotiationLayer.PayByTheBook)
            {
                _reservations.AddRange(GwpFieldArrestLines.BuildPaymentReservations(_desire, ResolveArgumentStrength()));
                _reservations[0].SpokenLine = _voice.Alternate("demand_" + _desire, GwpOffenderDesires.Line(_desire));
                foreach (var task in _reservations) task.SpokenLine = _voice.Question(task.SpokenLine);
                return;
            }

            _reservations.AddRange(GwpFieldArrestLines.BuildReservations(
                _offender,
                _crime,
                CurrentStandingTier(),
                HasBeenTakenBefore,
                ResolveArgumentStrength()));
            foreach (var task in _reservations) task.SpokenLine = _voice.Question(task.SpokenLine);
        }

        private PersuasionTask? CurrentReservation() =>
            _reservations.FirstOrDefault(task => !task.Options.All(option => option.IsBlocked));

        private bool UnmetReservationCondition()
        {
            PersuasionTask? task = CurrentReservation();
            if (task == null) return false;

            bool retry = ReferenceEquals(task, _lastArgumentTask)
                && (_lastResult == PersuasionOptionResult.Failure || _lastResult == PersuasionOptionResult.Miss);
            MBTextManager.SetTextVariable("GWP_ARREST_RESERVATION", retry
                ? (_layer == NegotiationLayer.PayByTheBook
                    ? GwpText.Create("{=gwp_fa_retry_payment}I still stand by my terms. Have you another proposal?")
                    : GwpText.Create("{=gwp_fa_retry_authority}That is not enough for me to accept your authority. Have you another reason?"))
                : task.SpokenLine ?? new TextObject(string.Empty));
            for (int index = 0; index < ArgumentsPerReservation; index++)
            {
                TextObject line = index < task.Options.Count
                    ? task.Options[index].Line
                    : new TextObject(string.Empty);
                MBTextManager.SetTextVariable("GWP_ARREST_ARGUMENT_" + index, line);
            }

            return true;
        }

        private bool ArgumentCondition(int index)
        {
            PersuasionTask? task = CurrentReservation();
            return task != null && index < task.Options.Count && !task.Options[index].IsBlocked;
        }

        private PersuasionOptionArgs? SetupArgument(int index)
        {
            PersuasionTask? task = CurrentReservation();
            return task != null && index < task.Options.Count ? task.Options[index] : null;
        }

        private void ArgumentConsequence(int index)
        {
            var task = CurrentReservation();
            if (task == null || index >= task.Options.Count) return;
            _lastArgumentTask = task;
            _lastArgument = task.Options[index];
            _pendingArgument = task.Options[index];
            // Native ConversationSentence.RunConsequence commits once AFTER this
            // callback. Never roll or block another reservation here.
        }

        private void OnArgumentCommitted(Tuple<PersuasionOptionArgs, PersuasionOptionResult> outcome)
        {
            if (_pendingArgument == null || !ReferenceEquals(outcome.Item1, _pendingArgument)) return;
            var option = _pendingArgument;
            _pendingArgument = null;
            _lastResult = outcome.Item2;
            _voice.NextArgument();
            if (_lastResult == PersuasionOptionResult.CriticalFailure)
                foreach (var reservation in _reservations) reservation.BlockAllOptions();
            else if (option.CanMoveToTheNextReservation &&
                (_lastResult == PersuasionOptionResult.Success || _lastResult == PersuasionOptionResult.CriticalSuccess))
                _lastArgumentTask?.BlockAllOptions();
            
        }

        /// <summary>
        /// Only a met goal counts. Running out of arguments is not agreement - that was the
        /// bug that had beaten Wardens collecting fines anyway.
        /// </summary>
        private bool PersuasionSatisfiedCondition() =>
            ConversationManager.GetPersuasionIsActive()
            && ConversationManager.GetPersuasionProgressSatisfied();

        /// <summary>
        /// The stock reaction line is empty unless someone fills PERSUASION_REACTION. A
        /// failed argument gets this reservation's own rebuff; a critical failure ends the
        /// whole conversation by blocking every remaining option.
        /// </summary>
        private bool RetryArgumentReactionCondition()
        {
            if (_lastResult != PersuasionOptionResult.Failure && _lastResult != PersuasionOptionResult.Miss) return false;
            if (_lastArgumentTask == null || !ReferenceEquals(CurrentReservation(), _lastArgumentTask)) return false;
            UnmetReservationCondition();
            return ArgumentReactionCondition();
        }

        private bool ArgumentReactionCondition()
        {
            try
            {
                PersuasionOptionResult result = _lastResult;
                PersuasionTask? task = _lastArgumentTask;

                if (result == PersuasionOptionResult.CriticalFailure)
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", GwpText.Create("{=gwp_fa_critical_rebuff}Enough. I will hear no more of this."));
                }
                else if ((result == PersuasionOptionResult.Failure
                     || result == PersuasionOptionResult.Miss)
                    && task?.ImmediateFailLine != null)
                {
                    MBTextManager.SetTextVariable("PERSUASION_REACTION", GwpFieldArrestLines.ArgumentRebuff(_lastArgument!, _desire, _layer == NegotiationLayer.PayByTheBook));
                }
                else
                {
                    MBTextManager.SetTextVariable(
                        "PERSUASION_REACTION",
                        _voice.Line("success_" + GwpFieldArrestLines.Read(_offender)));
                }
            }
            catch
            {
                MBTextManager.SetTextVariable(
                    "PERSUASION_REACTION",
                    GwpText.Create("{=gwp_fieldarrest_reaction_fallback}..."));
            }

            return true;
        }

        #endregion

        #region 结果

        /// <summary>
        /// 谈过之后（无论哪一层、成败如何，也包括缴清之后）玩家仍握着执法权，
        /// 随时可以回头拿人。这是执法权给玩家的余地，代价将来再算。
        /// </summary>
        private void DismissConsequence()
        {
            EndPersuasionConsequence();
            LeaveEncounterPeacefully();
            ClearState();
        }

        private void OpenCollection(bool full)
        {
            if (_crime == null || _offender == null || _offenderParty == null || !_enforcementAccepted
                || (full && !_paymentAccepted)) return;
            EndPersuasionConsequence();
            full = full || _paymentAccepted;
            _collectionFull = full;
            _collectionWasBroke = GwpAssetPayment.Wealth(_offender, _offenderParty.Party) < EnsureFine();
            _collectionEmpty = GwpAssetPayment.Wealth(_offender, _offenderParty.Party) <= 0;
            _collection = null;
            if (_collectionEmpty) return;
            int limit = full ? EnsureFine() : _desire == GwpOffenderDesire.BribeTheWarden ? BribeOffer : EnsureFine() * GwpOffenderDesires.OfferedSharePercent(_desire) / 100;
            _collection = new GwpAssetPayment(_offender, Hero.MainHero, _offenderParty.Party, MobileParty.MainParty.Party, limit);
            BarterManager manager = Campaign.Current.BarterManager;
            var original = manager.BarterBegin;
            try
            {
                manager.BarterBegin = data => {
                    _collection.PrepareCatalogue(data);
                    original?.Invoke(data);
                };
                
                manager.StartBarterOffer(Hero.MainHero, _offender, MobileParty.MainParty.Party, _offenderParty.Party,
                    null, (item, data, obj) => _collection.IsAgreement(item), 0, false, _collection.Entries);
            }
            catch (Exception ex) { GwpAiDiagnostics.WriteFieldArrest("COLLECTION_OPEN_FAILED", ex.ToString()); }
            finally { manager.BarterBegin = original; }
        }

        private void FinishCollection()
        {
            if (_collection?.Applied != true) return;
            int paid = _collection.Paid;
            _collection = null;
            if (!_collectionFull && _desire == GwpOffenderDesire.BribeTheWarden) { BribeConsequence(paid); return; }
            if (!_collectionFull && _desire == GwpOffenderDesire.DeedsOnly) PunishOwnMen();
            Settle(paid, "SETTLED_COLLECTION", paid, _collectionWasBroke);
        }

        /// <summary>谈崩之后玩家选择照他的方案办。</summary>
        private void ExecuteDesireConsequence()
        {
            if (_crime == null || _offender == null || !_enforcementAccepted) return;

            switch (_desire)
            {
                case GwpOffenderDesire.RequestGrace:
                    GrantGrace();
                    return;
                case GwpOffenderDesire.SurrenderSelf:
                    SurrenderConsequence();
                    return;
                case GwpOffenderDesire.DemandDuel:
                    DuelConsequence();
                    return;
                default:
                    GwpAiDiagnostics.WriteFieldArrest("DESIRE_ROUTE_REJECTED", "desire=" + _desire);
                    return;
            }
        }

        /// <summary>
        /// 收钱、抵账、销案。缴足的把案底一并抹平，缴不足的把差额留在他名下——
        /// 案子销了，人并不干净。
        /// </summary>
        private void Settle(int demanded, string stage, int? prepaid = null, bool? wasBroke = null)
        {
            if (_crime == null || _offender == null) return;

            EndPersuasionConsequence();
            if (_duelPaymentHero == _offender.StringId) _duelPaymentHero = string.Empty;

            int owed = EnsureFine();
            int standingBefore = GetNegativeStanding(_crime);
            int collected = prepaid ?? PoliceResourceManager.CollectFieldFine(_offender, demanded);

            LeaveEncounterPeacefully();

            // 犯人的账在这里就清，按他**实际交了多少**抵负声望——他已经当众交代过了。
            // 玩家手上这笔钱要不要如实上交，是玩家与灰袍之间的另一本账。
            GwpFieldReportLedger.Instance?.RecordSettlement(
                _offender, owed, collected, CalculateBaseFine(_crime),
                wasBroke ?? _offender.Gold < owed - collected);

            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                ?.NotifyCaseSettledInField(_offender, collected, owed, standingBefore);

            RegisterFieldEnforcement(_offender, _crime);
            CrimePool.CloseCaseSettledInField(_crime);
            PlayerState.ChangeReputation(GwpTuning.FieldArrest.StandingGainOnSubmit);

            GwpAiDiagnostics.WriteFieldArrest(
                stage,
                "offender=" + (_offender.StringId ?? "-") +
                "; owed=" + owed +
                "; demanded=" + demanded +
                "; collected=" + collected +
                "; standingBefore=" + standingBefore +
                "; desire=" + _desire);

            InformationManager.DisplayMessage(new InformationMessage(
                collected >= owed
                    ? GwpText.Get("{=gwp_fieldarrest_msg_fine}{VAR_1} paid {VAR_2}. The money belongs to the order and must be handed in to a Warden lord.", "VAR_1", _offender.Name?.ToString() ?? string.Empty, "VAR_2", collected.ToString())
                    : GwpText.Get("{=gwp_fieldarrest_msg_partial}{VAR_1} paid {VAR_2} against an assessed {VAR_3}. The pursuit has ended. Report the payment; the record is adjusted only when you hand it in.", "VAR_1", _offender.Name?.ToString() ?? string.Empty, "VAR_2", collected.ToString(), "VAR_3", owed.ToString()),
                Colors.Green));

            ClearState();
        }

        /// <summary>
        /// 荣誉高的兑现方式：钱留给部下，人跟你走。原版会让失去领主的队伍自行
        /// 解散、士兵各自返回最近的定居点，这里不额外干预。
        /// </summary>
        private void SurrenderConsequence()
        {
            if (_crime == null || _offender == null) return;

            EndPersuasionConsequence();
            int assessed = EnsureFine();
            int standing = GetNegativeStanding(_crime);

            try { TakePrisonerAction.Apply(MobileParty.MainParty.Party, _offender); }
            catch (Exception ex)
            {
                GwpAiDiagnostics.WriteFieldArrest("SURRENDER_FAILED", ex.ToString());
            }
            if (!_offender.IsPrisoner || _offender.PartyBelongedToAsPrisoner != MobileParty.MainParty.Party)
            {
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_case_capture_failed}The offender has not entered your custody. The case remains open."), Colors.Red));
                LeaveEncounterPeacefully();
                ClearState();
                return;
            }
            HeroCrimeStats history = CrimePool.GetOrCreateHistory(_offender);
            history.NegativeStanding = 0;
            history.CrimeKillProgress = 0;

            LeaveEncounterPeacefully();

            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                ?.NotifyCaseClosedByCapture(_offender, assessed, standing);
            RegisterFieldEnforcement(_offender, _crime);
            CrimePool.CloseCaseSettledInField(_crime);
            PlayerState.ChangeReputation(GwpTuning.FieldArrest.StandingGainOnSubmit);

            GwpAiDiagnostics.WriteFieldArrest(
                "SETTLED_SURRENDER",
                "offender=" + (_offender.StringId ?? "-") +
                "; assessed=" + assessed + "; standing=" + standing);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_fieldarrest_msg_surrender}{VAR_1} has given himself up. His men will make their own way home; hand the prisoner to a Grey Warden lord.",
                    "VAR_1", _offender.Name?.ToString() ?? string.Empty),
                Colors.Green));

            ClearState();
        }

        /// <summary>
        /// 荣誉低的兑现方式：塞一点私钱把案子抹掉。钱直接进玩家自己的口袋，不入
        /// 司法公库，案子销掉，但他名下的案底一点没少——公库没收到钱，人就没赎。
        /// </summary>
        private void BribeConsequence(int? prepaid = null)
        {
            if (_crime == null || _offender == null) return;

            EndPersuasionConsequence();
            int owed = EnsureFine();
            int purse = Math.Max(1, owed * GwpTuning.FieldArrest.BribeSharePercent / 100);
            int taken = prepaid ?? PoliceResourceManager.CollectFieldFine(_offender, purse);

            LeaveEncounterPeacefully();
            GwpFieldReportLedger.Instance?.RecordSettlement(
                _offender, owed, 0, CalculateBaseFine(_crime), false, taken);
            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                ?.NotifyCaseSettledInField(_offender, 0, owed, GetNegativeStanding(_crime));
            RegisterFieldEnforcement(_offender, _crime);
            CrimePool.CloseCaseSettledInField(_crime);

            GwpAiDiagnostics.WriteFieldArrest(
                "SETTLED_BRIBE",
                "offender=" + (_offender.StringId ?? "-") +
                "; owed=" + owed + "; bribe=" + taken + "; standingUntouched=" + GetNegativeStanding(_crime));

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_fieldarrest_msg_bribe}{VAR_1} privately gave you property worth {VAR_2} denars. This is not a regular fine payment; the commission still needs reporting.",
                    "VAR_1", _offender.Name?.ToString() ?? string.Empty, "VAR_2", taken.ToString()),
                Colors.Yellow));

            ClearState();
        }

        /// <summary>
        /// 执法单挑正在等结果的那名罪犯。单挑要离开对话、走完一整场战斗才回来，
        /// 所以这一条必须跨对话保留，但不必跨存档——打到一半存档退出等于放弃。
        /// </summary>
        private static string _pendingDuelOffenderId = string.Empty;
        private static int _pendingDuelFine;
        internal static bool IsEnforcementDuelPending(Hero? hero) => hero != null
            && !string.IsNullOrEmpty(_pendingDuelOffenderId) && hero.StringId == _pendingDuelOffenderId;

        internal static void CancelPendingEnforcementDuel(Hero? hero)
        {
            if (!IsEnforcementDuelPending(hero)) return;
            _pendingDuelOffenderId = string.Empty;
            _pendingDuelFine = 0;
        }

        /// <summary>
        /// 切磋管线打完之后回到这里。他赢就照他开出的条件免罚，玩家赢就全额照缴。
        /// 无论哪一种，玩家都仍然可以回头找他动手——执法权不因一场比试而消失。
        /// </summary>
        internal static void OnEnforcementDuelFinished(Hero? opponent, bool playerWon)
        {
            if (opponent == null
                || string.IsNullOrEmpty(_pendingDuelOffenderId)
                || !string.Equals(opponent.StringId, _pendingDuelOffenderId, StringComparison.OrdinalIgnoreCase))
                return;

            int fine = _pendingDuelFine;
            _pendingDuelOffenderId = string.Empty;
            _pendingDuelFine = 0;

            MobileParty? party = opponent.PartyBelongedTo;
            CrimeRecord? crime = party == null ? null : CrimePool.GetByOffenderId(party.StringId);

            GwpAiDiagnostics.WriteFieldArrest(
                "DUEL_FINISHED",
                "offender=" + (opponent.StringId ?? "-") + "; playerWon=" + playerWon + "; fine=" + fine);

            if (crime?.HasOpenCase != true)
                return;

            if (!playerWon)
            {
                // 他赢了，按他自己开出的条件免罚。案子就此销掉，案底照留。
                GwpFieldReportLedger.Instance?.RecordSettlement(opponent, fine, 0, CalculateBaseFine(crime), false);
                Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()
                    ?.NotifyCaseSettledInField(opponent, 0, fine, GetNegativeStanding(crime));
                RegisterFieldEnforcement(opponent, crime);
                CrimePool.CloseCaseSettledInField(crime);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_fieldarrest_msg_duel_lost}{VAR_1} won the bout. The fine is waived, but the record against {VAR_1} stands.",
                        "VAR_1", opponent.Name?.ToString() ?? string.Empty),
                    Colors.Yellow));
                return;
            }

            var behavior = Campaign.Current?.GetCampaignBehavior<GwpFieldArrestBehavior>();
            if (behavior != null) behavior._duelPaymentHero = opponent.StringId ?? string.Empty;
            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_duel_payment_pending}You won. Leave the duel scene, then inspect the offender's property to collect payment."), Colors.Green));
        }

        private string _duelPaymentHero = string.Empty;
        internal void PrepareDuelPropertyCollection()
        {
            Hero? hero = Hero.OneToOneConversationHero;
            if (hero == null || hero.StringId != _duelPaymentHero) return;
            PrepareFieldArrest();
            _enforcementAccepted = true;
            _paymentAccepted = true;
            _desire = GwpOffenderDesire.PayInFull;
        }

        /// <summary>勇武高的兑现方式：单挑。交给既有的野外切磋流程。</summary>
        private void DuelConsequence()
        {
            if (_offender == null) return;

            GwpAiDiagnostics.WriteFieldArrest(
                "DUEL_REQUESTED", "offender=" + (_offender.StringId ?? "-") + "; fine=" + EnsureFine());

            EndPersuasionConsequence();
            _pendingDuelOffenderId = _offender.StringId ?? string.Empty;
            _pendingDuelFine = EnsureFine();
            if (!GreyWardenSparringBehavior.RequestFieldDuel(_offender))
            {
                _pendingDuelOffenderId = string.Empty;
                _pendingDuelFine = 0;
                GwpAiDiagnostics.WriteFieldArrest("DUEL_REQUEST_FAILED", "offender=" + _offender.StringId);
                InformationManager.DisplayMessage(new InformationMessage(
                    GwpText.Get("{=gwp_case_duel_failed}The duel could not begin. The case remains open."), Colors.Red));
                LeaveEncounterPeacefully();
            }
            ClearState();
        }

        /// <summary>
        /// 仁慈低的附带动作：他不认那些死掉的村民，转手拿几个自己的兵出气。
        /// </summary>
        private void PunishOwnMen()
        {
            MobileParty? party = _offenderParty;
            if (party?.MemberRoster == null) return;

            int toKill = GwpTuning.FieldArrest.DeedsOnlyTroopsKilled;
            foreach (TroopRosterElement element in party.MemberRoster.GetTroopRoster().ToList())
            {
                if (toKill <= 0) break;
                if (element.Character?.IsHero != false) continue;

                int take = Math.Min(toKill, element.Number);
                party.MemberRoster.AddToCounts(element.Character, -take);
                toKill -= take;
            }

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_fieldarrest_msg_own_men}He has the men he blames dragged out and cut down in front of you."),
                Colors.Red));
        }

        /// <summary>抗拒执法，或者玩家自己决定动手。</summary>
        private bool SetVoice(string key, string original)
        {
            MBTextManager.SetTextVariable("GWP_VOICE_" + key.ToUpperInvariant(), _voice.Alternate(key, GwpText.Create(original)));
            return true;
        }

        private bool PrepareFightReply()
        {
            MBTextManager.SetTextVariable("GWP_FIELD_FIGHT_REPLY",
                _voice.Line((HasReceivedCash ? "betray_" : "fight_") + GwpFieldArrestLines.Read(_offender)));
            return true;
        }

        private void AttackConsequence()
        {
            EndPersuasionConsequence();

            GwpAiDiagnostics.WriteFieldArrest(
                "ATTACK",
                "offender=" + (_offender?.StringId ?? "-") +
                "; fine=" + _fine + "; desire=" + _desire);

            Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>()?.NotifyCaseCombatStarting(_offender);
            _pendingBattleParty = _offenderParty;
            ClearState();
        }

        private bool AcceptDesireCondition()
        {
            MBTextManager.SetTextVariable(
                GwpTextKeys.FieldArrestAccept,
                _paymentAccepted || GwpOffenderDesires.IsFullPayment(_desire)
                    ? GwpText.Create("{=gwp_fa_accept_count}Show me the money and goods. We will agree what they cover.")
                    : GwpOffenderDesires.PlayerAcceptLine(_desire));
            return true;
        }

        /// <summary>
        /// 结案的对话必须主动结束遭遇，否则关掉对话就直接落进战斗准备菜单，
        /// 刚缴完罚金的人会站在开战界面里。
        /// </summary>
        private static void LeaveEncounterPeacefully()
        {
            try
            {
                if (PlayerEncounter.Current == null) return;
                PlayerEncounter.LeaveEncounter = true;
                PlayerEncounter.Finish(false);
            }
            catch
            {
            }
        }

        /// <summary>
        /// 只清这一次谈判的状态。待开打的目标不在此列——它要活到对话结束之后。
        /// </summary>
        private void ClearState()
        {
#if GWP_DIAGNOSTICS
            _testDesireApplied = false;
#endif
            _enforcementAccepted = false;
            _paymentAccepted = false;
            _lastArgumentTask = null;
            _lastArgument = null;
            _pendingArgument = null;
            _refusesToTalk = false;
            _collection = null;
            _collectionEmpty = false;
            _reservations.Clear();
            _layer = NegotiationLayer.None;
            _crime = null;
            _offender = null;
            _offenderParty = null;
            _fine = 0;
            _desire = GwpOffenderDesire.PayInFull;
        }

        #endregion
    }

    /// <summary>
    /// A refused arrest is settled on the spot, not by a declaration of war. The player is
    /// already standing in front of the man, so the encounter is simply turned into the
    /// battle it has become; the offender's kingdom is never dragged in, which is the
    /// whole point of the Wardens policing individuals rather than states.
    /// </summary>
    internal static class GwpFieldArrestHostility
    {
        internal static void StartBattleNow(MobileParty? offender)
        {
            if (offender == null || MobileParty.MainParty == null)
            {
                GwpAiDiagnostics.WriteFieldArrest("ATTACK_LAUNCH", "result=no_target");
                return;
            }

            try
            {
                if (MobileParty.MainParty.MapEvent != null)
                {
                    bool sameBattle = ReferenceEquals(MobileParty.MainParty.MapEvent, offender.MapEvent);
                    if (sameBattle) PlayerEncounter.Update();
                    GwpAiDiagnostics.WriteFieldArrest("ATTACK_LAUNCH", "target=" + offender.StringId
                        + "; result=" + (sameBattle ? "existing_battle" : "other_battle_active"));
                    return;
                }
                // 原版自己强行开打（劫匪任务、帮派火并）走的就是这三步：把当前遭遇
                // 重挂成"他是守方、玩家是攻方"，开打，再让遭遇自己跑一次 Update。
                // 此前用的 StartPartyEncounter + StartBattle 在对话刚结束、遭遇还没
                // 成形时起不来，玩家被原样弹回地图，于是同一次执法留下两条 ATTACK。
                bool hadEncounter = PlayerEncounter.Current != null;
                if (!hadEncounter)
                    EncounterManager.StartPartyEncounter(
                        MobileParty.MainParty.Party, offender.Party);

                PlayerEncounter.RestartPlayerEncounter(
                    offender.Party, MobileParty.MainParty.Party,
                    forcePlayerOutFromSettlement: false,
                    isPlayerEncounterRestartedForRaid: false);
                PlayerEncounter.StartBattle();
                PlayerEncounter.Update();

                GwpAiDiagnostics.WriteFieldArrest(
                    "ATTACK_LAUNCH",
                    "target=" + (offender.LeaderHero?.StringId ?? offender.StringId ?? "-") +
                    "; hadEncounter=" + hadEncounter +
                    "; encounterNow=" + (PlayerEncounter.Current != null) +
                    "; mapEvent=" + (MobileParty.MainParty.MapEvent != null) +
                    "; result=started");
            }
            catch (Exception exception)
            {
                // 拉不起战斗时保持原状：宁可让玩家自己动手，也不要在这里改变阵营关系。
                // 但必须留下痕迹——上一轮就是因为这里静默吞掉，日志里只看得到两条
                // ATTACK，看不出为什么没打起来。
                GwpAiDiagnostics.WriteFieldArrest(
                    "ATTACK_LAUNCH",
                    "target=" + (offender.LeaderHero?.StringId ?? offender.StringId ?? "-") +
                    "; result=threw; error=" + exception.GetType().Name +
                    ": " + (exception.Message ?? string.Empty));
            }
        }
    }
}
