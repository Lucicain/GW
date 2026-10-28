using System;
using System.Linq;
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
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _guardRound.OnCommitted);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _hallRound.OnCommitted);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _choiceRound.OnCommitted);
        }

        /// <summary>
        /// 经城镇菜单的原版「离开」出城同样是离城：已控制住的目标此时交付，不因没走搜捕菜单的出口而丢失
        /// （用户裁定 2026-10-21「离城才交付」；实机反馈 2026-10-28）。原版先清空所在聚落再发本事件，
        /// 所以按事件给出的聚落核对。敌对时只能杀出城门，不走这里；被俘时原版也先设俘虏再离城，不算押人离城。
        /// </summary>
        private void OnSettlementLeft(MobileParty party, Settlement settlement)
        {
            if (party != MobileParty.MainParty || Hero.MainHero.IsPrisoner || !SearchActive || _searchWar
                || settlement.StringId != _searchSettlementId) return;
            FinishSearch(true, leftSettlement: settlement);
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
            RegisterSearchMenu(starter);
        }

        private bool SearchOptionCondition(MenuCallbackArgs args)
        {
            Hero? target = SearchHere ? SearchTarget : Bounty?.CitySearchTargetIn(Settlement.CurrentSettlement);
            if (target == null) return false;
            MBTextManager.SetTextVariable("GWP_CITY_SEARCH_OPTION",
                GwpText.Create(GwpTextCatalog.gwp_city_search_option, "TARGET", target.Name));
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            if (!SearchHere && Hero.MainHero.IsWounded)
            {
                args.IsEnabled = false;
                args.Tooltip = GwpText.Create(GwpTextCatalog.gwp_city_search_wounded);
            }
            return true;
        }

        private static void SearchOptionConsequence(MenuCallbackArgs args) => GameMenu.SwitchToMenu(SearchMenu);

        private static void SelectSearchTroops(MenuCallbackArgs args)
        {
            Settlement? settlement = Settlement.CurrentSettlement;
            Hero? target = Bounty?.CitySearchTargetIn(settlement);
            if (settlement == null || target == null
                || Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>().CanBeginSearch(target) != true) return;

            // 带兵上限同原版亲自突击藏身处，选兵也用原版那个界面（用户裁定 2026-10-21）。
            // 灰袍办案队已到身边时，队里的人单列、锁定必带，占这个上限里的名额（用户裁定 2026-10-28）。
            MobileParty main = MobileParty.MainParty;
            TroopRoster? support = Enforcement?.CitySearchSupport(target)?.MemberRoster;
            int max = Campaign.Current.Models.BanditDensityModel.GetMaximumTroopCountForHideoutMission(main, true);
            TroopRoster initial = TroopRoster.CreateDummyTroopRoster();
            initial.Add(MobilePartyHelper.GetStrongestAndPriorTroops(main, max - (support?.TotalHealthyCount ?? 0), includePlayer: true));
            args.MenuContext.OpenTroopSelection(GwpCitySearchSupportSelection.Register(main.MemberRoster, support), initial, CanTakeAlong,
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

        /// <summary>双方性格对论点的影响交给原版；声望与兵力只调本轮难度，不再另挪论点档位。</summary>
        private static Func<GwpPersuasionRound.Argument, PersuasionArgumentStrength> StrengthAgainst(CharacterObject opponent)
        {
            return argument => Campaign.Current.Models.PersuasionModel.GetArgumentStrengthBasedOnTargetTraits(
                opponent, new[] { Tuple.Create(argument.Trait, argument.Effect == TraitEffect.Positive ? 1 : -1) });
        }

        /// <summary>三轮共用：以声望20和现场势均力敌为基准，优势/劣势有限度地改变原版难度输入。</summary>
        internal static float SearchDifficulty(int reputation, float ours, float theirs)
        {
            float standing = Math.Max(-GwpTuning.CitySearch.PersuasionStandingLimit,
                Math.Min(GwpTuning.CitySearch.PersuasionStandingLimit,
                    ((float)reputation - GwpTuning.CitySearch.PersuasionStandingBaseline) * GwpTuning.CitySearch.PersuasionStandingStep));
            // 没有一方参战者时是确定的极端优势；双方都无人时只按声望，不把0/0交给Log。
            float power = ours <= 0f ? (theirs <= 0f ? 0f : -GwpTuning.CitySearch.PersuasionPowerLimit)
                : theirs <= 0f ? GwpTuning.CitySearch.PersuasionPowerLimit
                : (float)Math.Log(ours / theirs, 2d) * GwpTuning.CitySearch.PersuasionPowerStep;
            power = Math.Max(-GwpTuning.CitySearch.PersuasionPowerLimit, Math.Min(GwpTuning.CitySearch.PersuasionPowerLimit, power));
            return GwpTuning.CitySearch.PersuasionBaseDifficulty - standing - power;
        }

        private void BeginSearchPersuasion(GwpPersuasionRound round, PersuasionTask[] tasks)
        {
            (float ours, float theirs) = Scene!.CombatBalance();
            int reputation = GwpRuntimeState.Player.Reputation;
            float difficulty = SearchDifficulty(reputation, ours, theirs);
            round.Begin(tasks, difficulty);
        }

        private void BeginGuardPersuasion()
        {
            var strength = StrengthAgainst(CharacterObject.OneToOneConversationCharacter);
            TextObject immediate = GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_immediate);
            TextObject final = GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_final);
            TextObject later = GwpText.Create(GwpTextCatalog.gwp_cs_guard_try_later);
            // 两道顾虑考八种说法：统御、魅力、诡道、交易，再是战术、管理、统御、诡道，各配一种性格
            // （用户裁定 2026-10-22：多考验玩家各方面素质）。
            PersuasionTask[] tasks =
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
            };
            BeginSearchPersuasion(_guardRound, tasks);
        }

        internal void AdjustSearchDifficulty(PersuasionOptionArgs option, ref float difficultyMultiplier)
        {
            GwpPersuasionRound? round = _guardRound.Owns(option) ? _guardRound
                : _hallRound.Owns(option) ? _hallRound : _choiceRound.Owns(option) ? _choiceRound : null;
            if (round != null) difficultyMultiplier = round.DifficultyMultiplier;
        }

        /// <summary>保留既定的大失败下限，技能、性格、特长和游戏难度加成仍由原版计算。</summary>
        internal void AdjustSearchChances(PersuasionOptionArgs option, ref float success, ref float criticalSuccess,
            ref float criticalFailure, ref float failure)
        {
            if (!_guardRound.Owns(option) && !_hallRound.Owns(option) && !_choiceRound.Owns(option)) return;
            EnsureCriticalFailure(option.GivesCriticalSuccess, ref success, ref criticalSuccess, ref criticalFailure, ref failure);
        }

        internal static void EnsureCriticalFailure(bool givesCriticalSuccess,
            ref float success, ref float criticalSuccess, ref float criticalFailure, ref float failure)
        {
            criticalFailure = Math.Max(criticalFailure,
                GwpTuning.CitySearch.CriticalFailureFloor * (givesCriticalSuccess ? 2f : 1f));
            float totalSuccess = success + criticalSuccess;
            if (totalSuccess > 1f - criticalFailure)
            {
                float scale = (1f - criticalFailure) / totalSuccess;
                success *= scale;
                criticalSuccess *= scale;
            }
            failure = Math.Max(0f, 1f - success - criticalSuccess - criticalFailure);
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
