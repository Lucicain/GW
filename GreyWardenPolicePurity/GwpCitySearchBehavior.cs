using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation.Persuasion;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 玩家带兵进城搜捕没带部队、住在城里的委托目标（用户裁定 2026-10-21）。
    /// 本行为管城镇/城堡菜单入口、选兵和卫兵盘问；场景里的事由 <see cref="GwpCitySearchMissionController"/> 管。
    /// </summary>
    internal sealed class GwpCitySearchBehavior : CampaignBehaviorBase
    {
        private readonly GwpPersuasionRound _guardRound = new GwpPersuasionRound("GWP_CS_GUARD");

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.PersuasionProgressCommittedEvent.AddNonSerializedListener(this, _guardRound.OnCommitted);
        }

        // 搜捕只在任务场景里进行，场景里不能存档；离开场景这次搜捕就结束，没有进行中的状态要存。
        public override void SyncData(IDataStore dataStore)
        {
        }

        private static PlayerBountyBehavior? Bounty => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
        private static GwpCitySearchMissionController? Scene => GwpCitySearchMissionController.Current;

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            foreach (string menu in new[] { "town", "castle" })
                starter.AddGameMenuOption(menu, "gwp_city_search_" + menu, "{GWP_CITY_SEARCH_OPTION}",
                    SearchOptionCondition, SearchOptionConsequence, isLeave: false, index: 1);
            RegisterGuardDialogue(starter);
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
            GwpAiDiagnostics.WriteCitySearch("SELECT_TROOPS", "settlement=" + settlement.StringId
                + "; target=" + target.StringId + "; max=" + max
                + "; healthy=" + party.MemberRoster.TotalHealthyCount);
            args.MenuContext.OpenTroopSelection(party.MemberRoster, initial, CanTakeAlong,
                roster => GwpCitySearchMission.Open(settlement, target, roster), max, 1);
        }

        private static bool CanTakeAlong(CharacterObject character) =>
            !character.IsPlayerCharacter && !character.IsNotTransferableInHideouts;

        #region 卫兵盘问：一次简单说服

        private void RegisterGuardDialogue(CampaignGameStarter starter)
        {
            starter.AddDialogLine("gwp_cs_guard_challenge", "start", "gwp_cs_guard_options",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_challenge), () => Scene?.IsGuardTalk == true, null, 200);
            starter.AddPlayerLine("gwp_cs_explain", "gwp_cs_guard_options", "gwp_cs_guard_next",
                "{=!}{GWP_CS_EXPLAIN}", PrepareExplain, BeginGuardPersuasion);
            starter.AddPlayerLine("gwp_cs_will_leave", "gwp_cs_guard_options", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_will_leave), null, () => Scene?.OnGuardDecision(false, "player_leaves"));

            // 先看是否已说服，再给下一道顾虑，都没有就是谈崩：对话总有出口。
            starter.AddDialogLine("gwp_cs_guard_pass", "gwp_cs_guard_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_pass), () => _guardRound.IsSatisfied, () => FinishGuard(true), 110);
            starter.AddDialogLine("gwp_cs_guard_reservation", "gwp_cs_guard_next", "gwp_cs_guard_argument",
                "{=!}{GWP_CS_GUARD_RESERVATION}", _guardRound.PrepareReservation, null, 105);
            starter.AddDialogLine("gwp_cs_guard_refuse", "gwp_cs_guard_next", "close_window",
                GwpText.Get(GwpTextCatalog.gwp_cs_guard_refuse), () => _guardRound.IsFailed, () => FinishGuard(false), 100);
            _guardRound.RegisterArguments(starter, "gwp_cs_guard_argument", "gwp_cs_guard_argument", "gwp_cs_guard_reaction");
            starter.AddDialogLine("gwp_cs_guard_reaction", "gwp_cs_guard_reaction", "gwp_cs_guard_next",
                "{=!}{PERSUASION_REACTION}", () => _guardRound.PrepareReaction(
                    GwpTextCatalog.gwp_cs_guard_reaction_success, GwpTextCatalog.gwp_cs_guard_reaction_fail,
                    GwpTextCatalog.gwp_cs_guard_reaction_critical), null);
        }

        private static bool PrepareExplain()
        {
            MBTextManager.SetTextVariable("GWP_CS_EXPLAIN", GwpText.Create(GwpTextCatalog.gwp_cs_explain,
                "TARGET", Scene?.TargetName ?? new TextObject(string.Empty)));
            return true;
        }

        /// <summary>
        /// 卫兵盘问的难易随玩家在灰袍的声望（用户裁定 2026-10-21）；分档沿用当面拘捕看玩家记录的那三档。
        /// </summary>
        internal static (PersuasionArgumentStrength Strength, PersuasionDifficulty Difficulty) GuardDifficulty(
            GwpFieldArrestBehavior.WardenStandingTier tier) => tier switch
        {
            GwpFieldArrestBehavior.WardenStandingTier.Respected => (PersuasionArgumentStrength.VeryEasy, PersuasionDifficulty.VeryEasy),
            GwpFieldArrestBehavior.WardenStandingTier.UnderCharge => (PersuasionArgumentStrength.Hard, PersuasionDifficulty.Medium),
            _ => (PersuasionArgumentStrength.Easy, PersuasionDifficulty.Easy)
        };

        private void BeginGuardPersuasion()
        {
            GwpFieldArrestBehavior.WardenStandingTier tier = GwpFieldArrestBehavior.CurrentStandingTier();
            (PersuasionArgumentStrength strength, PersuasionDifficulty difficulty) = GuardDifficulty(tier);
            GwpAiDiagnostics.WriteCitySearch("GUARD_PERSUASION", "tier=" + tier + "; strength=" + strength
                + "; difficulty=" + difficulty);
            _guardRound.Begin(new[]
            {
                GwpPersuasionRound.Task(GwpText.Create(GwpTextCatalog.gwp_cs_guard_reservation),
                    strength,
                    GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_immediate),
                    GwpText.Create(GwpTextCatalog.gwp_cs_guard_fail_final),
                    GwpText.Create(GwpTextCatalog.gwp_cs_guard_try_later),
                    GwpText.Create(GwpTextCatalog.gwp_cs_arg_lead),
                    GwpText.Create(GwpTextCatalog.gwp_cs_arg_charm),
                    GwpText.Create(GwpTextCatalog.gwp_cs_arg_rogue),
                    GwpText.Create(GwpTextCatalog.gwp_cs_arg_trade))
            }, difficulty);
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
