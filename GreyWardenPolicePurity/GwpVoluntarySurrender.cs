using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>和平投案统一退团、押人，再交给原版失去领主的部队收尾。</summary>
    internal static class GwpVoluntarySurrender
    {
        internal static bool CanCapture(Hero offender)
        {
            MobileParty? source = offender.PartyBelongedTo;
            return source?.MapEvent == null && source?.IsMainParty != true &&
                   (source?.Army?.LeaderParty != source ||
                    source?.Army?.Parties.Any(member => member.MapEvent != null) != true);
        }

        internal static bool TryCapture(PartyBase capturer, Hero offender)
        {
            if (!CanCapture(offender)) return false;
            MobileParty? source = offender.PartyBelongedTo;
            if (source?.Army is Army army)
            {
                // 英雄仍在时拆编制，避免退团事件/军团定时器读到空英雄。
                if (army.LeaderParty == source)
                    DisbandArmyAction.ApplyByLeaderPartyRemoved(army);
                else
                    source.Army = null;
            }
            TakePrisonerAction.Apply(capturer, offender);
            if (!offender.IsPrisoner || offender.PartyBelongedToAsPrisoner != capturer)
                return false;
            if (source?.IsActive == true && source.LeaderHero == null)
                DisbandPartyAction.StartDisband(source);
            return true;
        }
    }
}
