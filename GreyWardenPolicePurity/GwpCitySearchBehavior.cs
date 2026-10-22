using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家带兵进城搜捕没带部队、住在城里的委托目标（用户裁定 2026-10-21）。
    /// 本行为管城镇/城堡菜单入口、选兵和卫兵盘问；场景里的事由 <see cref="GwpCitySearchMissionController"/> 管。
    /// </summary>
    internal sealed partial class GwpCitySearchBehavior : CampaignBehaviorBase
    {
        private readonly GwpPersuasionRound _guardRound = new GwpPersuasionRound("GWP_CS_GUARD");

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _guardRound.OnCommitted);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _hallRound.OnCommitted);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _choiceRound.OnCommitted);
        }

        // 场景内不能存档；切场景时短暂回到地图，所以下一场景、随行人和伤情也要保存。
        public override void SyncData(IDataStore dataStore)
        {
            SyncSearch(dataStore);
        }

        private static PlayerBountyBehavior? Bounty => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
        private static GwpCitySearchMissionController? Scene => GwpCitySearchMissionController.Current;

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            foreach (string menu in new[] { "town", "castle" })
                starter.AddGameMenuOption(menu, "gwp_city_search_" + menu, "{GWP_CITY_SEARCH_OPTION}",
                    SearchOptionCondition, SearchOptionConsequence, isLeave: false, index: 1);
            RegisterGuardDialogue(starter);
            RegisterHallDialogue(starter);
            if (SearchActive) _openPending = true;
        }

        private static bool SearchOptionCondition(MenuCallbackArgs args)
        {
            Hero? target = Bounty?.CitySearchTargetIn(Settlement.CurrentSettlement);
            if (target == null) return false;
            MBTextManager.SetTextVariable("GWP_CITY_SEARCH_OPTION",
                GwpText.Create(GwpTextCatalog.gwp_city_search_option, "TARGET", target.Name));
            args.optionLeaveType = GameMenuOption.LeaveType.Mission;
            if (Hero.MainHero.IsWounded)
            {
                args.IsEnabled = false;
                args.Tooltip = GwpText.Create(GwpTextCatalog.gwp_city_search_wounded);
            }
            return true;
        }

        private static void SearchOptionConsequence(MenuCallbackArgs args)
        {
            Settlement? settlement = Settlement.CurrentSettlement;
            Hero? target = Bounty?.CitySearchTargetIn(settlement);
            MobileParty? party = MobileParty.MainParty;
            if (settlement == null || target == null || party == null) return;

            // 带兵上限同原版亲自突击藏身处，选兵也用原版那个界面（用户裁定 2026-10-21）。
            int max = Campaign.Current.Models.BanditDensityModel.GetMaximumTroopCountForHideoutMission(party, true);
            TroopRoster initial = TroopRoster.CreateDummyTroopRoster();
            initial.Add(MobilePartyHelper.GetStrongestAndPriorTroops(party, max, includePlayer: true));
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("SELECT_TROOPS", "settlement=" + settlement.StringId
                + "; target=" + target.StringId + "; max=" + max
                + "; healthy=" + party.MemberRoster.TotalHealthyCount);
#endif
            args.MenuContext.OpenTroopSelection(party.MemberRoster, initial, CanTakeAlong,
                roster => Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>().BeginSearch(settlement, target, roster), max, 1);
        }

        private static bool CanTakeAlong(CharacterObject character) =>
            !character.IsPlayerCharacter && !character.IsNotTransferableInHideouts;

        #region 卫兵盘问：两道关卡的说服，谈崩后玩家选打或撤

        private void RegisterGuardDialogue(CampaignGameStarter starter)
        {
            starter.AddDialogLine("gwp_cs_guard_challenge", "start", "gwp_cs_guard_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_challenge), () => Scene?.IsGuardTalk == true, null, 200);
            starter.AddPlayerLine("gwp_cs_explain", "gwp_cs_guard_options", "gwp_cs_guard_next",
                "{=!}{GWP_CS_EXPLAIN}", PrepareExplain, BeginGuardPersuasion);
            starter.AddPlayerLine("gwp_cs_will_leave", "gwp_cs_guard_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_will_leave), null, () => Scene?.OnPromisedToLeave());

            // 先看是否已说服，再给下一道顾虑，都没有就是谈崩：对话总有出口。
            starter.AddDialogLine("gwp_cs_guard_pass", "gwp_cs_guard_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_pass), () => _guardRound.IsSatisfied, () => FinishGuard(true), 110);
            starter.AddDialogLine("gwp_cs_guard_reservation", "gwp_cs_guard_next", "gwp_cs_guard_argument",
                "{=!}{GWP_CS_GUARD_RESERVATION}", _guardRound.PrepareReservation, null, 105);
            starter.AddDialogLine("gwp_cs_guard_refuse", "gwp_cs_guard_next", "gwp_cs_refused_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_refuse), () => _guardRound.IsFailed, () => FinishGuard(false), 100);
            _guardRound.RegisterArguments(starter, "gwp_cs_guard_argument", "gwp_cs_guard_argument", "gwp_cs_guard_reaction");
            starter.AddDialogLine("gwp_cs_guard_reaction", "gwp_cs_guard_reaction", "gwp_cs_guard_next",
                "{=!}{PERSUASION_REACTION}", () => _guardRound.PrepareReaction(
                    GwpTextCatalog.gwp_cs_guard_reaction_success, GwpTextCatalog.gwp_cs_guard_reaction_fail,
                    GwpTextCatalog.gwp_cs_guard_reaction_critical), null);

            // 谈崩后玩家二选一：打进去，或撤出城（用户裁定 2026-10-22）。
            starter.AddPlayerLine("gwp_cs_choose_fight", "gwp_cs_refused_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choose_fight), null, () => Scene?.OnChoseFight());
            starter.AddPlayerLine("gwp_cs_choose_withdraw", "gwp_cs_refused_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_choose_withdraw), null, () => Scene?.OnChoseWithdraw());
        }

        private static bool PrepareExplain()
        {
            MBTextManager.SetTextVariable("GWP_CS_EXPLAIN", GwpText.Create(GwpTextCatalog.gwp_cs_explain,
                "TARGET", Scene?.TargetName ?? new TextObject(string.Empty)));
            return true;
        }

        /// <summary>
        /// 卫兵盘问论点的强弱随玩家在灰袍的声望（用户裁定 2026-10-21），分档沿用当面拘捕看玩家记录的那三档；
        /// 整体比最初的「简单」难一档（用户裁定 2026-10-22）。原版难度档几乎不影响成败，不再分档。
        /// </summary>
        internal static PersuasionArgumentStrength GuardStrength(GwpFieldArrestBehavior.WardenStandingTier tier) => tier switch
        {
            GwpFieldArrestBehavior.WardenStandingTier.Respected => PersuasionArgumentStrength.Easy,
            GwpFieldArrestBehavior.WardenStandingTier.UnderCharge => PersuasionArgumentStrength.Hard,
            _ => PersuasionArgumentStrength.Normal
        };

        /// <summary>城主家族族长对玩家的关系、玩家在该势力的犯罪评级折成的成功率加减，开谈时定下，整轮不变。</summary>
        private float _guardSituation;

        private void BeginGuardPersuasion()
        {
            GwpFieldArrestBehavior.WardenStandingTier tier = GwpFieldArrestBehavior.CurrentStandingTier();
            PersuasionArgumentStrength strength = GuardStrength(tier);
            Settlement? settlement = Scene?.Settlement;
            Hero? owner = settlement?.OwnerClan?.Leader;
            int relation = owner == null || owner == Hero.MainHero ? 0 : Hero.MainHero.GetRelation(owner);
            float crime = settlement?.MapFaction?.MainHeroCrimeRating ?? 0f;
            _guardSituation = relation * GwpTuning.CitySearch.RelationStep - crime * GwpTuning.CitySearch.CrimeStep;
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("GUARD_PERSUASION", "tier=" + tier + "; strength=" + strength
                + "; relation=" + relation + "; crime=" + crime.ToString("0") + "; situation=" + _guardSituation.ToString("0.00"));
#endif
            TextObject immediate = GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_immediate);
            TextObject final = GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_final);
            TextObject later = GwpText.Create(GwpTextCatalog.gwp_cs_guard_try_later);
            // 两道顾虑考八种说法：统御、魅力、诡道、交易，再是战术、管理、统御、诡道，各配一种性格
            // （用户裁定 2026-10-22：多考验玩家各方面素质）。
            _guardRound.Begin(new[]
            {
                GwpPersuasionRound.Task(GwpText.Create(GwpTextCatalog.gwp_cs_guard_reservation), strength, immediate, final, later,
                    new GwpPersuasionRound.Argument(DefaultSkills.Leadership, DefaultTraits.Honor, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_lead)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Charm, DefaultTraits.Mercy, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_charm)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Roguery, DefaultTraits.Valor, TraitEffect.Negative, true,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_rogue)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Trade, DefaultTraits.Calculating, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_trade))),
                GwpPersuasionRound.Task(GwpText.Create(GwpTextCatalog.gwp_cs_guard_reservation_vouch), strength, immediate, final, later,
                    new GwpPersuasionRound.Argument(DefaultSkills.Tactics, DefaultTraits.Calculating, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_tactics)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Steward, DefaultTraits.Honor, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_steward)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Leadership, DefaultTraits.Valor, TraitEffect.Positive, false,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_valor)),
                    new GwpPersuasionRound.Argument(DefaultSkills.Roguery, DefaultTraits.Honor, TraitEffect.Negative, true,
                        GwpText.Create(GwpTextCatalog.gwp_cs_arg_blackmail)))
            }, PersuasionDifficulty.Medium);
        }

        /// <summary>原版掷骰前改写卫兵这轮说法的成败率；别的说服原样不动。</summary>
        internal void AdjustGuardChances(PersuasionOptionArgs option, ref float success, ref float criticalSuccess,
            ref float criticalFailure, ref float failure)
        {
            bool hall = _hallRound.Owns(option) || _choiceRound.Owns(option);
            if (!_guardRound.Owns(option) && !hall) return;
            int trait = option.TraitUsed == null ? 0 : Hero.MainHero.GetTraitLevel(option.TraitUsed)
                * (option.TraitEffect == TraitEffect.Positive ? 1 : -1);
            GuardOdds(success + criticalSuccess, criticalFailure, option.GivesCriticalSuccess,
                Hero.MainHero.GetSkillValue(option.SkillUsed), trait, hall ? HallSituation() : _guardSituation,
                out success, out criticalSuccess, out criticalFailure, out failure);
        }

        /// <summary>
        /// 原版成功率（已按论点强弱折算）乘技能系数，加性格与处境，夹在上下限内；大失败取原版与保底的较大者，
        /// 以大成功为赌注的说法保底翻倍；四项相加为 1，先挤掉普通失败、再挤成功。
        /// 原版技能加成要满级才生效（整数相除），难度档几乎不起作用（C#反编译 v1.4.8 DefaultPersuasionModel）。
        /// </summary>
        internal static void GuardOdds(float nativeSuccess, float nativeCriticalFailure, bool givesCriticalSuccess,
            int skill, int trait, float situation,
            out float success, out float criticalSuccess, out float criticalFailure, out float failure)
        {
            float skillFactor = Math.Min(GwpTuning.CitySearch.SkillFactorCap,
                GwpTuning.CitySearch.SkillFactorAtZero + skill / GwpTuning.CitySearch.SkillPointsPerFactor);
            float win = Math.Max(GwpTuning.CitySearch.SuccessFloor, Math.Min(GwpTuning.CitySearch.SuccessCeiling,
                nativeSuccess * skillFactor + Math.Max(-2, Math.Min(2, trait)) * GwpTuning.CitySearch.TraitStep + situation));
            criticalFailure = Math.Max(nativeCriticalFailure,
                GwpTuning.CitySearch.CriticalFailureFloor * (givesCriticalSuccess ? 2f : 1f));
            win = Math.Min(win, 1f - criticalFailure);
            failure = 1f - win - criticalFailure;
            success = givesCriticalSuccess ? 0f : win;
            criticalSuccess = givesCriticalSuccess ? win : 0f;
        }

        private void FinishGuard(bool allowed)
        {
            string result = _guardRound.LastResult;
            _guardRound.End();
            Scene?.OnGuardDecision(allowed, result);
        }

        #endregion
    }
}
