using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GreyWardenPolicePurity
{
    public class PoliceClanTierModel : DefaultClanTierModel
    {
        public override int GetPartyLimitForTier(Clan clan, int clanTierToCheck)
        {
            int baseLimit = base.GetPartyLimitForTier(clan, clanTierToCheck);
            if (!IsPoliceClan(clan))
                return baseLimit;

            int policeCommanderLimit = clan.Heroes.Count(IsPoliceCommander);
            return Math.Max(baseLimit, Math.Max(1, policeCommanderLimit));
        }

        private static bool IsPoliceClan(Clan? clan)
        {
            return string.Equals(
                clan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPoliceCommander(Hero? hero)
        {
            if (!GwpCommon.IsGreyWardenLord(hero))
                return false;

            if (hero == null || hero.IsDead || hero.IsDisabled || hero.IsChild)
                return false;

            return hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
        }
    }
}
