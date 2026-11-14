using System;
using System.Linq;
using GreyWardenPolicePurity.Core.Troops;
using HarmonyLib;
using SandBox.View.Map.Navigation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GreyWardenPolicePurity
{
    internal static class GwpPlayerWardenTroops
    {
        internal static bool IsPlayerWardenTroop(PartyBase? party, CharacterObject? troop) =>
            party?.MobileParty?.IsMainParty == true && GwpCommon.IsGreyWardenTroop(troop);

        internal static int Count(TroopRoster roster) =>
            roster.GetTroopRoster().Where(entry => GwpCommon.IsGreyWardenTroop(entry.Character)).Sum(entry => entry.Number);

        internal const int MaximumCapacity = PlayerBehaviorPool.MaxReputation * WardenReputationRules.TroopsPerReputation;

        internal static int Capacity(int reputationUnits) => WardenReputationRules.CapacityFromUnits(reputationUnits);

        internal static int RemainingSlots(TroopRoster roster) =>
            WardenReputationRules.RemainingSlots(Count(roster), PlayerBehaviorPool.ReputationUnits);

        internal static int PendingUpgrades(PartyScreenLogic screen) => screen.CurrentData.UpgradedTroopsHistory
            .Where(upgrade => GwpCommon.IsGreyWardenTroop(upgrade.Item1)).Sum(upgrade => upgrade.Item3);

        internal static TextObject CapacityMessage(int reputationUnits) =>
            GwpText.Create(GwpTextCatalog.gwp_player_warden_capacity, "CAPACITY", Capacity(reputationUnits));

        internal static TextObject? GetConfirmRestriction(PartyScreenLogic screen, TroopRoster initialRoster, int upgrades)
        {
            if (upgrades > PlayerBehaviorPool.AffordableWardenUpgrades)
                return GwpText.Create(GwpTextCatalog.gwp_warden_upgrade_insufficient);

            int previous = Count(initialRoster);
            int current = Count(screen.CurrentData.RightMemberRoster);
            int reputationUnits = PlayerBehaviorPool.ReputationUnits -
                PlayerBehaviorPool.WardenUpgradeCost(upgrades) * WardenReputationRules.TroopUnitsPerPoint;
            return current > Math.Max(previous, Capacity(reputationUnits)) ? CapacityMessage(reputationUnits) : null;
        }

        internal static void DiscardUnconfirmedChanges(PartyScreenLogic screen, TroopRoster initialRoster)
        {
            if (screen.RightOwnerParty?.MobileParty?.IsMainParty != true) return;
            if (PendingUpgrades(screen) > 0 || GetConfirmRestriction(screen, initialRoster, 0) != null)
                screen.Reset(fromCancel: true);
        }
    }

    [HarmonyPatch(typeof(MapNavigationHelper), nameof(MapNavigationHelper.SwitchToANewScreen))]
    internal static class GwpPlayerWardenPartyNavigationPatch
    {
        private static readonly AccessTools.FieldRef<PartyScreenLogic, PartyScreenData> InitialData =
            AccessTools.FieldRefAccess<PartyScreenLogic, PartyScreenData>("_initialData");

        private static void Prefix()
        {
            // Refusing to apply changes from the navigation bar pops the state
            // without calling PartyScreenLogic.Reset or DoneLogic.
            if (!MapNavigationHelper.IsMapTopScreen() &&
                GameStateManager.Current.ActiveState is PartyState { PartyScreenLogic: { } screen })
                GwpPlayerWardenTroops.DiscardUnconfirmedChanges(screen, InitialData(screen).RightMemberRoster);
        }
    }

    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.IsDoneActive))]
    internal static class GwpPlayerWardenDoneButtonPatch
    {
        private static readonly AccessTools.FieldRef<PartyScreenLogic, string> DoneReason =
            AccessTools.FieldRefAccess<PartyScreenLogic, string>("<DoneReasonString>k__BackingField");

        private static void Postfix(PartyScreenLogic __instance, PartyScreenData ____initialData,
            ref bool __result)
        {
            if (!__result || __instance.RightOwnerParty?.MobileParty?.IsMainParty != true) return;

            TextObject? reason = GwpPlayerWardenTroops.GetConfirmRestriction(__instance, ____initialData.RightMemberRoster,
                GwpPlayerWardenTroops.PendingUpgrades(__instance));
            if (reason == null) return;
            DoneReason(__instance) = reason.ToString();
            __result = false;
        }
    }

    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.ValidateCommand))]
    internal static class GwpPlayerWardenUpgradeCommandPatch
    {
        private static bool Prefix(PartyScreenLogic __instance, PartyScreenLogic.PartyCommand command, ref bool __result)
        {
            if (command.Code != PartyScreenLogic.PartyCommandCode.UpgradeTroop ||
                command.RosterSide != PartyScreenLogic.PartyRosterSide.Right ||
                !GwpPlayerWardenTroops.IsPlayerWardenTroop(__instance.RightOwnerParty, command.Character))
                return true;

            int available = PlayerBehaviorPool.AffordableWardenUpgrades - GwpPlayerWardenTroops.PendingUpgrades(__instance);
            if (command.TotalNumber > 0 && command.TotalNumber <= available) return true;

            MBInformationManager.AddQuickInformation(GwpText.Create(GwpTextCatalog.gwp_warden_upgrade_insufficient));
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(PartyScreenLogic), nameof(PartyScreenLogic.DoneLogic))]
    internal static class GwpPlayerWardenPartyConfirmPatch
    {
        private static bool Prefix(PartyScreenLogic __instance, PartyScreenData ____initialData,
            ref int __state, ref bool __result)
        {
            __state = 0;
            if (__instance.RightOwnerParty?.MobileParty?.IsMainParty != true) return true;

            int upgrades = GwpPlayerWardenTroops.PendingUpgrades(__instance);
            TextObject? reason = GwpPlayerWardenTroops.GetConfirmRestriction(__instance, ____initialData.RightMemberRoster, upgrades);
            if (reason != null)
            {
                MBInformationManager.AddQuickInformation(reason);
                __result = false;
                return false;
            }

            __state = upgrades;
            return true;
        }

        private static void Postfix(bool __result, int __state)
        {
            if (__result && __state > 0) PlayerBehaviorPool.PayForWardenUpgrades(__state);
        }
    }

    [HarmonyPatch(typeof(PartyScreenLogic))]
    internal static class GwpPlayerWardenPrisonerRecruitmentPatch
    {
        [HarmonyPostfix, HarmonyPatch(nameof(PartyScreenLogic.IsPrisonerRecruitable))]
        private static void FilterRecruitable(PartyScreenLogic __instance, CharacterObject character, ref bool __result)
        {
            if (__result && GwpPlayerWardenTroops.IsPlayerWardenTroop(__instance.RightOwnerParty, character))
                __result = false;
        }

        [HarmonyPostfix, HarmonyPatch(nameof(PartyScreenLogic.GetTroopRecruitableAmount))]
        private static void FilterRecruitableCount(PartyScreenLogic __instance, CharacterObject troop, ref int __result)
        {
            if (__result > 0 && GwpPlayerWardenTroops.IsPlayerWardenTroop(__instance.RightOwnerParty, troop))
                __result = 0;
        }
    }

    [HarmonyPatch(typeof(PartyCharacterVM), nameof(PartyCharacterVM.InitializeUpgrades))]
    internal static class GwpPlayerWardenUpgradeDisplayPatch
    {
        private static void Postfix(PartyCharacterVM __instance)
        {
            if (__instance.Side != PartyScreenLogic.PartyRosterSide.Right ||
                __instance.Type != PartyScreenLogic.TroopType.Member || !__instance.IsUpgradableTroop ||
                Game.Current.GameStateManager.ActiveState is not PartyState state ||
                !GwpPlayerWardenTroops.IsPlayerWardenTroop(state.PartyScreenLogic.RightOwnerParty, __instance.Character))
                return;

            int pending = GwpPlayerWardenTroops.PendingUpgrades(state.PartyScreenLogic);
            int available = Math.Max(0, PlayerBehaviorPool.AffordableWardenUpgrades - pending);
            foreach (UpgradeTargetVM upgrade in __instance.Upgrades)
            {
                upgrade.AvailableUpgrades = Math.Min(upgrade.AvailableUpgrades, available);
                upgrade.IsInsufficient |= upgrade.AvailableUpgrades == 0;
            }
            __instance.NumOfUpgradeableTroops = __instance.Upgrades.Select(upgrade => upgrade.AvailableUpgrades).DefaultIfEmpty().Max();
            __instance.IsTroopUpgradable &= __instance.NumOfUpgradeableTroops > 0;
        }
    }

    [HarmonyPatch(typeof(UpgradeTargetVM), "GetHint")]
    internal static class GwpPlayerWardenUpgradeHintPatch
    {
        private static void Postfix(CharacterObject ____originalCharacter, ref string __result)
        {
            if (Game.Current.GameStateManager.ActiveState is not PartyState state || state.PartyScreenLogic.IsTroopUpgradesDisabled ||
                !GwpPlayerWardenTroops.IsPlayerWardenTroop(state.PartyScreenLogic.RightOwnerParty, ____originalCharacter)) return;

            int pending = GwpPlayerWardenTroops.PendingUpgrades(state.PartyScreenLogic);
            string charge = PlayerBehaviorPool.AffordableWardenUpgrades <= pending
                ? GwpText.Get(GwpTextCatalog.gwp_warden_upgrade_insufficient)
                : GwpText.Get(GwpTextCatalog.gwp_warden_upgrade_hint,
                    "COUNT", WardenReputationRules.UpgradesPerPoint, "COST", PlayerBehaviorPool.WardenUpgradeCost(pending));
            __result = string.IsNullOrEmpty(__result) ? charge : __result + Environment.NewLine + charge;
        }
    }
}
