using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 灰袍家族不采用原版的“低武器技能即非战斗员”家族分工。
    /// 所有成年的在世灰袍成员都始终按战斗员处理；其他家族完全沿用原版判定。
    /// </summary>
    public sealed class PoliceHeroCreationModel : DefaultHeroCreationModel
    {
        public override bool IsHeroCombatant(Hero hero)
        {
            if (IsAdultGreyWarden(hero))
                return true;

            return base.IsHeroCombatant(hero);
        }

        private static bool IsAdultGreyWarden(Hero? hero)
        {
            if (hero == null || hero.IsDead || hero.IsDisabled || hero.IsChild)
                return false;

            if (!string.Equals(hero.Clan?.StringId, PoliceStats.PoliceClanId,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            return hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
        }
    }
}
