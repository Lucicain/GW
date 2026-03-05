using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 限制灰袍家族女性与 AI 家族 NPC 结婚。
    /// 允许与玩家主角结婚（如需全禁可改为直接返回 false）。
    /// </summary>
    public class PoliceMarriageModel : DefaultMarriageModel
    {
        private static bool IsGwFemale(Hero hero)
        {
            return hero != null
                && hero.IsFemale
                && hero.Clan != null
                && string.Equals(hero.Clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        public override bool IsCoupleSuitableForMarriage(Hero firstHero, Hero secondHero)
        {
            if (!base.IsCoupleSuitableForMarriage(firstHero, secondHero))
                return false;

            // 灰袍女性仅允许嫁给玩家主角，禁止与 AI NPC 结婚。
            if (IsGwFemale(firstHero) && secondHero != Hero.MainHero)
                return false;
            if (IsGwFemale(secondHero) && firstHero != Hero.MainHero)
                return false;

            return true;
        }
    }
}
