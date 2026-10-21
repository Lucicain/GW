using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;

namespace GreyWardenPolicePurity
{
    public sealed partial class GwpFireswornBehavior
    {
        // 沿用既有军械保存结构，只把领主的持有者ID改为人物ID；部队重建不改变所有权。
        private const string LordArmoryPrefix = "hero:";
        internal static string LordArmoryId(Hero hero) => LordArmoryPrefix + hero.StringId;

        private static MobileParty? FindArmoryParty(string holder)
        {
            if (!holder.StartsWith(LordArmoryPrefix, StringComparison.Ordinal)) return FindParty(holder);
            Hero? owner = Hero.Find(holder.Substring(LordArmoryPrefix.Length));
            MobileParty? party = owner?.PartyBelongedTo;
            return party?.IsActive == true && party.LeaderHero == owner ? party : null;
        }

        private string BindLordArmory(MobileParty party)
        {
            string holder = LordArmoryId(party.LeaderHero);
            // 旧档仍用部队ID；迁移实际物品，不复制、不清库存，也不改村庄或玩家的账。
            if (_armories.TryGetValue(party.StringId, out ItemRoster? old))
            {
                ItemsOf(_armories, holder).Add(old);
                _armories.Remove(party.StringId);
                _plans.Remove(party.StringId);
                _plans.Remove(holder);
            }
            return holder;
        }

        private void BindExistingLordArmories()
        {
            foreach (MobileParty party in MobileParty.All.Where(party => party.IsActive && !party.IsMainParty
                && party.IsLordParty && party.LeaderHero != null && GwpWardenRecruitment.IsWardenParty(party)))
                BindLordArmory(party);
            // 读档或中途加入战斗时同样重建归属；MapEvent及原版逐人名册本身已保存。
            foreach (MapEvent mapEvent in MobileParty.All.Select(party => party.MapEvent).Where(battle => battle != null).Distinct())
                CaptureArmoryBattleOwners(mapEvent);
        }
    }
}
