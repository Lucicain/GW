using System;
#if GWP_DIAGNOSTICS
using System.Linq;
#endif
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

namespace GreyWardenPolicePurity
{
    public sealed class PoliceAntiVanillaWarBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.WarDeclared.AddNonSerializedListener(this, OnWarDeclared);
            CampaignEvents.OnGameLoadFinishedEvent.AddNonSerializedListener(this, OnGameLoadFinished);
        }

        public override void SyncData(IDataStore dataStore) { }

        // 读档完成后恢复灰袍独立身份；不注册日结、周结或心跳轮询。
        // 此时原版对象、阵营与部队缓存已恢复，离国动作负责成员、军团、外交及封地收尾。
        // 依据：用户裁定 2026-10-06；1.4.8 Campaign / ChangeKingdomAction / Clan 反编译。
        private void OnGameLoadFinished()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan?.Kingdom == null) return;

#if GWP_DIAGNOSTICS
            string oldKingdomId = policeClan.Kingdom.StringId;
            GwpFaultTrace.Write("WARDEN_LOAD_LEAVE_STARTED", details:
                "old=" + oldKingdomId + "; mercenary=" + policeClan.IsUnderMercenaryService
                + "; fiefs=" + policeClan.Fiefs.Count
                + "; armies=" + policeClan.WarPartyComponents.Count(p => p.MobileParty.Army != null));
#endif
            if (policeClan.IsUnderMercenaryService)
                ChangeKingdomAction.ApplyByLeaveKingdomAsMercenary(policeClan, false);
            else
                ChangeKingdomAction.ApplyByLeaveKingdom(policeClan, false);
#if GWP_DIAGNOSTICS
            GwpFaultTrace.Write("WARDEN_LOAD_LEAVE_FINISHED", details:
                "old=" + oldKingdomId + "; new=" + (policeClan.Kingdom?.StringId ?? "none")
                + "; mercenary=" + policeClan.IsUnderMercenaryService
                + "; fiefs=" + policeClan.Fiefs.Count
                + "; armies=" + policeClan.WarPartyComponents.Count(p => p.MobileParty.Army != null));
#endif
        }

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
