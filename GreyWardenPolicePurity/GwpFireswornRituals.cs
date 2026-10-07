using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 集会传教与烈火责罚（docs/state/firesworn.md）。祭司来了才解锁这两件事（用户裁定 2026-10-07）。
    /// <list type="bullet">
    /// <item>集会传教：祭司先在本村传，有边际效应，最多传到祭司是本地最强的要人（用户裁定 2026-10-07）。
    /// 向外传教由信众队去做（GwpFireswornParties.cs）。</item>
    /// <item>烈火责罚：烧死两个同等级的人，生成一个同等级的灰袍兵，进守村队（用户裁定 2026-10-07，照辛迪加）。
    /// 本地传教还没到头时只能烧俘虏；到头了（祭司已是最强）才烧村里的原版民兵和村民这些韭菜。</item>
    /// </list>
    /// 频率参考比武大会（用户裁定）：比武大会每座城每季只有固定的一周能开、两次至少隔 15 天
    /// （C#反编译 v1.4.8 TournamentCampaignBehavior）。烧韭菜也是每座信众村每季一个固定的周、至少隔 15 天；
    /// 烧俘虏另算，隔 5 天就能烧。本地传教同一座村两次至少隔 15 天。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        private void UpdateRituals()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (Ritual ritual in _rituals.ToList())
            {
                Settlement village = Settlement.Find(ritual.VillageId);
                if (ritual.Kind == RitualKind.Damnation)
                {
                    if (now >= ritual.EndHour)
                        _rituals.Remove(ritual);
                    continue;
                }
                FaithVillage row = _villages[ritual.VillageId];
                if (!IsVillageQuiet(village) || FindLivingPriest(row) == null)
                {
                    _rituals.Remove(ritual);
                    GwpAiDiagnostics.WriteFaith("CONGREGATION_CANCELLED",
                        "village=" + village.StringId + "; quiet=" + IsVillageQuiet(village) +
                        "; priest=" + (FindLivingPriest(row) != null));
                    continue;
                }
                if (now >= ritual.EndHour)
                    CompleteCongregation(ritual, row, village);
            }
        }

        /// <summary>每天一次：有祭司、祭司还不是本地最强、离上次传教够 15 天的信众村，祭司在本村开集会传教。</summary>
        private void ScheduleCongregations()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values)
            {
                Settlement village = Settlement.Find(row.VillageId);
                if (FindLivingPriest(row) == null || !IsVillageQuiet(village) || HasRitual(village) ||
                    IsPriestStrongest(row, village) || Sworn(row) >= ZealotCap(village) ||
                    now - row.LastCongregation < GwpTuning.Firesworn.CongregationVillageIntervalDays * 24d)
                    continue;
                _rituals.Add(new Ritual
                {
                    VillageId = village.StringId,
                    Kind = RitualKind.Congregation,
                    EndHour = now + GwpTuning.Firesworn.CongregationHours
                });
                GwpAiDiagnostics.WriteFaith("CONGREGATION_STARTED",
                    "village=" + village.StringId + "; sworn=" + Sworn(row) + "; share=" + SwornShare(row, village).ToString("0.00") +
                    "; priestPower=" + FindLivingPriest(row)!.Power.ToString("0"));
            }
        }

        private void CompleteCongregation(Ritual ritual, FaithVillage row, Settlement village)
        {
            _rituals.Remove(ritual);
            int before = Sworn(row);
            int gain = AddToGuard(row, village, CharacterObject.Find(GwpIds.ZealotId),
                Math.Max(GwpTuning.Firesworn.CongregationMinimumGain,
                    (int)Math.Round(village.Village.Hearth * GwpTuning.Firesworn.CongregationHearthShare)));
            row.LastCongregation = CampaignTime.Now.ToHours;
            UpdatePriestPower(row);
            GwpSettlementNotices.Post(village, FindLivingPriest(row),
                GwpText.Get("{=gwp_notice_congregation}{COUNT} swore to the Eternal Fire", "COUNT", gain));
            GwpAiDiagnostics.WriteFaith("CONGREGATION_COMPLETED",
                "village=" + village.StringId + "; gain=" + gain + "; sworn=" + before + "->" + Sworn(row) +
                "; hearth=" + village.Village.Hearth.ToString("0") +
                "; priestPower=" + FindLivingPriest(row)!.Power.ToString("0") + "; strongest=" + IsPriestStrongest(row, village));
        }

        private bool HasRitual(Settlement settlement) => _rituals.Any(ritual =>
            string.Equals(ritual.VillageId, settlement.StringId, StringComparison.OrdinalIgnoreCase));

        // ---------------------------------------------------------------- 烈火责罚

        /// <summary>
        /// 每天一次，只在信众村（有祭司）。地牢里俘虏够数、离上次烧够 5 天就烧俘虏；
        /// 祭司已是本地最强（本地传教到头）时，在本村每季固定的那一周里、离上次烧够 15 天，按概率烧韭菜。
        /// </summary>
        private void ScheduleDamnations()
        {
            double now = CampaignTime.Now.ToHours;
            int weekOfSeason = CampaignTime.Now.GetDayOfSeason / CampaignTime.DaysInWeek;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement village = Settlement.Find(row.VillageId);
                if (FindLivingPriest(row) == null || HasRitual(village) || !IsVillageQuiet(village))
                    continue;
                double sinceLast = now - row.LastDamnation;
                int prisoners = _dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon)
                    ? dungeon.TotalManCount
                    : 0;
                if (prisoners >= GwpTuning.Firesworn.DamnationMinimumPrisoners &&
                    sinceLast >= GwpTuning.Firesworn.DamnationPrisonerIntervalDays * 24d)
                {
                    BurnPrisoners(row, village, dungeon!);
                    continue;
                }
                if (IsPriestStrongest(row, village) &&
                    weekOfSeason == StableDay(row.VillageId, CampaignTime.WeeksInSeason) &&
                    sinceLast >= GwpTuning.Firesworn.DamnationFestivalIntervalDays * 24d &&
                    village.Village.Hearth > GwpTuning.Firesworn.DirectZealotHearthFloor &&
                    MBRandom.RandomFloat < GwpTuning.Firesworn.DamnationFestivalDailyChance)
                    BurnLeeks(row, village);
            }
        }

        /// <summary>
        /// 烧死的人生成哪一种灰袍兵：同等级，没有这一级就往下取（用户裁定 2026-10-07）。灰袍兵线只有
        /// 2 级（狂信者）、3 级（新兵）、4 级（灰袍兵）、6 级（重步、弓手、骑士）；2 级以下也出狂信者——
        /// 烧死的人起码产生对应等级的人（用户裁定）。6 级按被烧的人的兵种出。
        /// 等级照原版 ceil((等级−5)/5)（C#反编译 v1.4.8 DefaultCharacterStatsModel.GetTier）。
        /// </summary>
        private static CharacterObject WardenForBurned(CharacterObject burned)
        {
            int tier = burned.Tier;
            string id = tier <= 2 ? GwpIds.ZealotId
                : tier == 3 ? GwpIds.NewRecruitId
                : tier <= 5 ? GwpIds.PoliceRecruitId
                : burned.IsRanged ? GwpIds.ArcherId
                : burned.IsMounted ? GwpIds.KnightId
                : GwpIds.HeavyInfantryId;
            return CharacterObject.Find(id);
        }

        /// <summary>
        /// 从 <paramref name="pool"/> 里成对地挑人烧：同一级（生成同一种灰袍兵）的两个人换一个灰袍兵，
        /// 落单的不烧。最多烧 <paramref name="limit"/> 人。返回每种灰袍兵生成几个，以及每个兵种烧了几个。
        /// </summary>
        private static Dictionary<CharacterObject, int> PickPairs(IEnumerable<TroopRosterElement> pool, int limit,
            Dictionary<CharacterObject, int> burnedByTroop)
        {
            Dictionary<CharacterObject, int> produced = new Dictionary<CharacterObject, int>();
            foreach (IGrouping<CharacterObject, TroopRosterElement> group in pool
                         .Where(element => !element.Character.IsHero && element.Number > 0)
                         .GroupBy(element => WardenForBurned(element.Character))
                         .OrderByDescending(entry => entry.Key.Tier))
            {
                int available = group.Sum(element => element.Number);
                int burn = Math.Min(available, limit) / 2 * 2;
                if (burn <= 0)
                    continue;
                limit -= burn;
                produced[group.Key] = burn / 2;
                int left = burn;
                foreach (TroopRosterElement element in group)
                {
                    int take = Math.Min(left, element.Number);
                    if (take <= 0)
                        break;
                    burnedByTroop.TryGetValue(element.Character, out int already);
                    burnedByTroop[element.Character] = already + take;
                    left -= take;
                }
            }
            return produced;
        }

        /// <summary>烧俘虏（用户裁定：俘虏是烧人的材料）。每烧 1 人从公库付钱（用户裁定：数据上只扣钱），公库不够就少烧。</summary>
        private void BurnPrisoners(FaithVillage row, Settlement village, TroopRoster dungeon)
        {
            int limit = Math.Min(GwpTuning.Firesworn.DamnationMaximumPrisoners,
                PoliceResourceManager.GetJudicialTreasuryBalance() / GwpTuning.Firesworn.DamnationCostPerHead);
            Dictionary<CharacterObject, int> burned = new Dictionary<CharacterObject, int>();
            Dictionary<CharacterObject, int> produced = PickPairs(dungeon.GetTroopRoster(), limit, burned);
            if (burned.Count == 0)
                return;
            foreach (KeyValuePair<CharacterObject, int> pair in burned)
            {
                int index = dungeon.FindIndexOfTroop(pair.Key);
                int healthy = dungeon.GetElementNumber(index) - dungeon.GetElementWoundedNumber(index);
                dungeon.AddToCounts(pair.Key, -pair.Value, false, -Math.Max(0, pair.Value - healthy));
            }
            FinishDamnation(row, village, "prisoners", burned.Values.Sum(), 0, produced, village.Village.Hearth);
        }

        /// <summary>
        /// 烧韭菜：本地传教到头以后，村里的原版民兵和村民成了烧人的原料（用户裁定 2026-10-07）。
        /// 先烧原版民兵，再烧村民；村民 1 人折 0.5 户，照原版村民队补人扣户的算法。
        /// </summary>
        private void BurnLeeks(FaithVillage row, Settlement village)
        {
            int limit = GwpTuning.Firesworn.DamnationUnbelieverBase +
                        (int)(village.Village.Hearth / GwpTuning.Firesworn.DamnationHearthPerUnbeliever);
            limit = Math.Min(limit, PoliceResourceManager.GetJudicialTreasuryBalance() /
                                    GwpTuning.Firesworn.DamnationCostPerHead);
            Dictionary<CharacterObject, int> burned = new Dictionary<CharacterObject, int>();
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            Dictionary<CharacterObject, int> produced = militia?.IsActive == true
                ? PickPairs(militia.MemberRoster.GetTroopRoster().Where(element => !IsGuardTroop(element.Character)), limit, burned)
                : new Dictionary<CharacterObject, int>();
            int militiaBurned = burned.Values.Sum();
            foreach (KeyValuePair<CharacterObject, int> pair in burned)
            {
                TroopRoster roster = militia!.MemberRoster;
                int index = roster.FindIndexOfTroop(pair.Key);
                int healthy = roster.GetElementNumber(index) - roster.GetElementWoundedNumber(index);
                roster.AddToCounts(pair.Key, -pair.Value, false, -Math.Max(0, pair.Value - healthy));
            }

            int villagers = (limit - militiaBurned) / 2 * 2;
            float hearthBefore = village.Village.Hearth;
            if (villagers > 0)
            {
                village.Village.Hearth -= villagers * GwpTuning.Firesworn.HearthPerPerson;
                CharacterObject zealot = WardenForBurned(village.Culture.Villager);
                produced.TryGetValue(zealot, out int already);
                produced[zealot] = already + villagers / 2;
            }
            if (militiaBurned + villagers <= 0)
                return;
            FinishDamnation(row, village, "leeks", militiaBurned, villagers, produced, hearthBefore);
        }

        private void FinishDamnation(FaithVillage row, Settlement village, string mode, int burnedTroops, int burnedVillagers,
            Dictionary<CharacterObject, int> produced, float hearthBefore)
        {
            int burned = burnedTroops + burnedVillagers;
            int cost = burned * GwpTuning.Firesworn.DamnationCostPerHead;
            PoliceResourceManager.SpendJudicialTreasury(cost);
            double now = CampaignTime.Now.ToHours;
            row.LastDamnation = now;
            int before = Sworn(row);
            int added = produced.Sum(pair => AddToGuard(row, village, pair.Key, pair.Value));
            UpdatePriestPower(row);
            GwpSettlementNotices.Post(village, FindLivingPriest(row),
                GwpText.Get("{=gwp_notice_damnation}Burned {BURNED}; {RISEN} rose as Wardens", "BURNED", burned, "RISEN", added));
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Damnation,
                EndHour = now + GwpTuning.Firesworn.DamnationLockHours
            });
            GwpAiDiagnostics.WriteFaith("DAMNATION",
                "village=" + village.StringId + "; mode=" + mode + "; burnedTroops=" + burnedTroops +
                "; burnedVillagers=" + burnedVillagers + "; cost=" + cost +
                "; produced=" + string.Join(",", produced.Select(pair => pair.Key.StringId + ":" + pair.Value)) +
                "; addedToGuard=" + added + "; sworn=" + before + "->" + Sworn(row) +
                "; hearth=" + hearthBefore.ToString("0.0") + "->" + village.Village.Hearth.ToString("0.0") +
                "; dungeonLeft=" + (_dungeons.TryGetValue(village.StringId, out TroopRoster? dungeon) ? dungeon.TotalManCount : 0));
        }

        /// <summary>仪式画面里主持人的名字：祭司，没有祭司就写村名。</summary>
        private string RitualHostName(Settlement village)
        {
            Hero? priest = _villages.TryGetValue(village.StringId, out FaithVillage? row) ? FindLivingPriest(row) : null;
            return priest?.Name?.ToString() ?? village.Name.ToString();
        }
    }
}
