using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal static class GwpPlayerIntervention
    {
        private const string PrepareMenu = "gwp_intervention_prepare";
        // Only a menu choice, with no troops, money or campaign work committed. Rebuilt on reopening, not saved.
        private static MapEvent? _preview;
        private static BattleSideEnum _side;
        private static bool _committing;

        internal static void Register(CampaignGameStarter starter)
        {
            _preview = null;
            _committing = false;
            // Native join_encounter has no Encounter overlay either: no player MapEvent exists yet.
            starter.AddGameMenu(PrepareMenu, "{GWP_INTERVENTION_TEXT}", Initialize);
            starter.AddGameMenuOption(PrepareMenu, "attack", GwpTextCatalog.gwp_intervention_attack,
                args => CanConfirm(args, false), args => Commit(args, false));
            starter.AddGameMenuOption(PrepareMenu, "send_troops", GwpTextCatalog.gwp_intervention_send,
                args => CanConfirm(args, true), args => Commit(args, true));
            starter.AddGameMenuOption(PrepareMenu, "back", GwpTextCatalog.gwp_intervention_back,
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Submenu; return true; },
                _ => Back());
            starter.AddGameMenuOption(PrepareMenu, "leave", GwpTextCatalog.gwp_intervention_leave,
                args => { args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                    return MobileParty.MainParty.Army == null || MobileParty.MainParty.Army.LeaderParty == MobileParty.MainParty; },
                _ => { _preview = null; PlayerEncounter.Finish(); MobileParty.MainParty.SetMoveModeHold(); }, isLeave: true);
        }

        internal static bool Eligible(MapEvent? battle) => battle != null && !battle.IsFinalized && !battle.HasWinner &&
            PlayerBehaviorMonitor.TryBuildForceJoinTexts(battle, out _, out _);

        private static bool CanPreview => MobileParty.MainParty?.IsActive == true && MobileParty.MainParty.MapEvent == null;

        internal static void ExtendChoice(MenuCallbackArgs args, BattleSideEnum side, ref bool visible)
        {
            MapEvent? battle = PlayerEncounter.EncounteredBattle;
            string? menu = Campaign.Current?.CurrentMenuContext?.GameMenu.StringId;
            if (!CanPreview || menu != "join_encounter" && menu != PrepareMenu || !Eligible(battle)) return;
            // The native condition has already applied health, sea/land and honour restrictions to args.
            visible = true;
            args.optionLeaveType = GameMenuOption.LeaveType.DefendAction;
        }

        internal static bool Select(BattleSideEnum side)
        {
            // Native reuses these callbacks for interrupted settlement waits. Keep those encounters native.
            if (_committing || !CanPreview || Campaign.Current?.CurrentMenuContext?.GameMenu.StringId != "join_encounter" ||
                !Eligible(PlayerEncounter.EncounteredBattle)) return true;
            _preview = PlayerEncounter.EncounteredBattle;
            _side = side;
            GameMenu.SwitchToMenu(PrepareMenu);
            return false;
        }

        private static bool Ready => _preview != null && ReferenceEquals(_preview, PlayerEncounter.EncounteredBattle) &&
            Eligible(_preview) && PlayerEncounter.Current != null && CanPreview;

        private static void Initialize(MenuCallbackArgs args)
        {
            if (!Ready) { Back(); return; }
            PartyBase ally = _preview!.GetLeaderParty(_side);
            PartyBase enemy = _preview.GetLeaderParty(_side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker);
            MBTextManager.SetTextVariable("GWP_INTERVENTION_TEXT", GwpText.Get(GwpTextCatalog.gwp_intervention_prepare,
                "ALLY", ally.Name, "ENEMY", enemy.Name,
                "ALLIES", _preview.GetMapEventSide(_side).TroopCount,
                "ENEMIES", _preview.GetMapEventSide(_side == BattleSideEnum.Attacker ? BattleSideEnum.Defender : BattleSideEnum.Attacker).TroopCount));
        }

        private static bool CanConfirm(MenuCallbackArgs args, bool simulate)
        {
            args.optionLeaveType = simulate ? GameMenuOption.LeaveType.OrderTroopsToAttack : GameMenuOption.LeaveType.Mission;
            if (!Ready) return false;
            args.IsEnabled = MobileParty.MainParty.Party.NumberOfHealthyMembers > 0 &&
                (simulate || !Hero.MainHero.IsWounded) && MobilePartyHelper.CanPartyAttackWithCurrentMorale(MobileParty.MainParty);
            if (!args.IsEnabled) args.Tooltip = GwpText.Create(GwpTextCatalog.gwp_intervention_unfit);
            return true;
        }

        private static void Back()
        {
            _preview = null;
            if (PlayerEncounter.EncounteredBattle?.IsFinalized == false && PlayerEncounter.EncounteredBattle.HasWinner == false)
                GameMenu.SwitchToMenu("join_encounter");
            else if (PlayerEncounter.Current != null) PlayerEncounter.Finish();
        }

        private static void Commit(MenuCallbackArgs args, bool simulate)
        {
            if (_preview == null) return;
            if (!Ready || !CanConfirm(args, simulate) || !args.IsEnabled) { Back(); return; }
            MapEvent battle = _preview!;
            BattleSideEnum side = _side;
            var native = Campaign.Current.GetCampaignBehavior<EncounterGameMenuBehavior>();
            // Recheck native non-diplomatic restrictions before committing, including naval raid ship capacity.
            string branch = side == BattleSideEnum.Attacker ? "attackers" : "defenders";
            var condition = AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_" + branch + "_on_condition");
            if (!(bool)condition.Invoke(native, new object[] { args }) || !args.IsEnabled) return;
            Campaign.Current.GetCampaignBehavior<PlayerBehaviorMonitor>().PrepareIntervention(battle, side);
            _preview = null;
            _committing = true;
            try
            {
                // Native handles raid-to-field conversion, party transfer, encounter roles and nearby reinforcements.
                AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_" + branch + "_on_consequence")
                    .Invoke(native, new object[] { args });
            }
            finally { _committing = false; }
            if (PlayerEncounter.Current == null || MobileParty.MainParty.MapEvent == null ||
                Campaign.Current.CurrentMenuContext?.GameMenu.StringId != "encounter") return;
            if (simulate) MenuHelper.EncounterOrderAttackConsequence(args);
            else MenuHelper.EncounterAttackConsequence(args);
        }
    }

    [HarmonyPatch]
    internal static class GwpInterventionChoicePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_condition");
            yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_condition");
        }
        private static void Postfix(MenuCallbackArgs args, ref bool __result, MethodBase __originalMethod) =>
            GwpPlayerIntervention.ExtendChoice(args, __originalMethod.Name.Contains("attackers") ? BattleSideEnum.Attacker : BattleSideEnum.Defender, ref __result);
    }

    [HarmonyPatch]
    internal static class GwpInterventionSelectionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_attackers_on_consequence");
            yield return AccessTools.Method(typeof(EncounterGameMenuBehavior), "game_menu_join_encounter_help_defenders_on_consequence");
        }
        private static bool Prefix(MethodBase __originalMethod) =>
            GwpPlayerIntervention.Select(__originalMethod.Name.Contains("attackers") ? BattleSideEnum.Attacker : BattleSideEnum.Defender);
    }
}
