using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace GreyWardenPolicePurity
{
    /// <summary>临时队沿用原版注册表的唯一编号，不另存计数器或随机重试。</summary>
    internal static class GwpPartyIdentity
    {
        internal static string Next(string prefix) => Next(Campaign.Current.CampaignObjectManager, prefix);

        internal static string Next(CampaignObjectManager manager, string prefix)
        {
            string candidate = manager.FindNextUniqueStringId<MobileParty>(prefix + "1");
            // 原版建队后再改 StringId 不更新 MaxCreatedPostfixIndex，原版返回值也须实际查重。
            // 每次碰撞严格推进后缀；有限注册表最多跳过其现有条目，不再随机重试。
            ulong suffix = ulong.Parse(candidate.Substring(prefix.Length));
            while (manager.Find<MobileParty>(candidate) != null)
                candidate = prefix + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
            return candidate;
        }
    }
}
