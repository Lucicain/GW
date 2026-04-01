using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GreyWardenPolicePurity
{
    public sealed class PoliceAntiVanillaWarBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnWarDeclared(
            IFaction faction1,
            IFaction faction2,
            DeclareWarAction.DeclareWarDetail detail)
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            IFaction? targetFaction = GetOtherFactionIfGreyWardenWar(policeClan, faction1, faction2);
            if (targetFaction == null) return;
            if (GwpPoliceWarReasonService.HasLegitimateWarReason(targetFaction)) return;

            GwpCommon.TrySetNeutral(policeClan, targetFaction);
        }

        private static IFaction? GetOtherFactionIfGreyWardenWar(
            Clan policeClan,
            IFaction faction1,
            IFaction faction2)
        {
            if (faction1 == null || faction2 == null) return null;

            IFaction? playerFaction = Clan.PlayerClan?.MapFaction;

            if (IsGreyWardenFaction(policeClan, faction1))
                return IsPlayerFaction(playerFaction, faction2) ? null : faction2;

            if (IsGreyWardenFaction(policeClan, faction2))
                return IsPlayerFaction(playerFaction, faction1) ? null : faction1;

            return null;
        }

        private static bool IsGreyWardenFaction(Clan policeClan, IFaction faction)
        {
            return faction == policeClan ||
                   string.Equals(faction.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPlayerFaction(IFaction? playerFaction, IFaction faction)
        {
            return playerFaction != null &&
                   string.Equals(playerFaction.StringId, faction.StringId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
