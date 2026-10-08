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
    /// <item>烈火责罚：俘虏、原版民兵烧死两个同等级的人，另一个信教——生成一个同等级的灰袍兵进守村队，一户村民信教
    /// （用户裁定 2026-10-07，照辛迪加）。本地传教还没到头时只能烧俘虏；到头了（祭司已是最强）才烧本村：
    /// 原版民兵（纯化守村队）和不信教的户（长期道路）。</item>
    /// </list>
    /// 本村传教、烧人各算各的：离上次同一件事满 10 天后每天固定概率，比比武大会勤一点（用户裁定 2026-10-07，见 IsRitualDay）。
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
                // 集会开了就传完，村子被打了才散；传教队到了开的集会本来就没有祭司（用户裁定 2026-10-08）。
                if (!IsVillageQuiet(village))
                {
                    _rituals.Remove(ritual);
                    GwpAiDiagnostics.WriteFaith("CONGREGATION_CANCELLED",
                        "village=" + village.StringId + "; quiet=False; priest=" + (FindLivingPriest(row) != null));
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
                    IsPriestStrongest(row, village) || row.Households >= HouseholdCap(village) ||
                    !IsRitualDay(row.LastCongregation, now))
                    continue;
                StartCongregation(row, village, "priest");
            }
        }

        /// <summary>
        /// 开一场集会传教，24 小时，锁村。祭司在本村开，传教队到了别的村也开（用户裁定 2026-10-08：传教队来了直接进传教流程，
        /// 和祭司本地传教一样）。没有祭司时主持人显示村名。
        /// </summary>
        private void StartCongregation(FaithVillage row, Settlement village, string host)
        {
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Congregation,
                EndHour = CampaignTime.Now.ToHours + GwpTuning.Firesworn.CongregationHours
            });
            GwpAiDiagnostics.WriteFaith("CONGREGATION_STARTED",
                "village=" + village.StringId + "; host=" + host + "; households=" + row.Households +
                "; share=" + SwornShare(row, village).ToString("0.00") +
                "; priestPower=" + (FindLivingPriest(row)?.Power.ToString("0") ?? "-"));
        }

        private void CompleteCongregation(Ritual ritual, FaithVillage row, Settlement village)
        {
            _rituals.Remove(ritual);
            int before = row.Households;
            int gain = Convert(row, village, CharacterObject.Find(GwpIds.ZealotId),
                Math.Max(GwpTuning.Firesworn.CongregationMinimumGain,
                    (int)Math.Round(village.Village.Hearth * GwpTuning.Firesworn.CongregationHearthShare)));
            row.LastCongregation = CampaignTime.Now.ToHours;
            UpdatePriestPower(row);
            GwpSettlementNotices.Post(village, FindLivingPriest(row),
                GwpText.Get("{=gwp_notice_congregation}{COUNT} swore to the Eternal Fire", "COUNT", gain));
            GwpAiDiagnostics.WriteFaith("CONGREGATION_COMPLETED",
                "village=" + village.StringId + "; gain=" + gain + "; households=" + before + "->" + row.Households + "; guard=" + Sworn(row) +
                "; hearth=" + village.Village.Hearth.ToString("0") +
                "; priestPower=" + (FindLivingPriest(row)?.Power.ToString("0") ?? "-") + "; strongest=" + IsPriestStrongest(row, village));
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
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement village = Settlement.Find(row.VillageId);
                if (FindLivingPriest(row) != null && !HasRitual(village) && IsVillageQuiet(village) &&
                    IsRitualDay(row.LastDamnation, now))
                    HoldDamnation(row, village);
            }
        }

        /// <summary>冒烟满了时辰，让地图重画这座村的图标，烟就散了。</summary>
        private void ClearExpiredSmoke()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (FaithVillage row in _villages.Values)
            {
                double since = now - row.LastDamnation;
                if (since >= GwpTuning.Firesworn.DamnationLockHours && since < GwpTuning.Firesworn.DamnationLockHours + 1d)
                    Settlement.Find(row.VillageId).Party.SetVisualAsDirty();
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

        /// <summary>
        /// 一场烈火责罚，每烧一个（人或户）公库付 GwpTuning.Firesworn.DamnationCostPerHead，不够就少烧（用户裁定）；
        /// 一场最多烧守村队那么多（人和户一起算，用户裁定 2026-10-08：烧人太厉害）：
        /// <list type="number">
        /// <item>地牢里的俘虏，成对烧。</item>
        /// <item>本村传教到头（祭司已是本地最强）以后才烧本村（用户裁定 2026-10-07）：
        /// <list type="bullet">
        /// <item>原版民兵，成对烧，一次最多 2 + 户数/200 个。这时信众村已不再出新民兵，烧掉旧的就是把守村队纯化，不是长期道路。</item>
        /// <item>不信教的户：现有信众户每 3 户烧 1 户。烧死的那户从户数里去掉，换来 1 户信教，所以每烧 1 户用掉 2 户不信教的，
        /// 先扣掉烧俘虏、民兵已经感化的；
        /// 不给守村队加兵——那些人家本来没有兵，守村队靠补兵自己长。一场涨得不多，要多烧几场（用户裁定 2026-10-07）。</item>
        /// </list></item>
        /// </list>
        /// 烧多少户按烧之前的信众户算。烧完村子在地图上冒烟（<see cref="GwpDamnationSmokePatch"/>）。
        /// </summary>
        private void HoldDamnation(FaithVillage row, Settlement village)
        {
            int budget = Math.Min(Sworn(row), PoliceResourceManager.GetJudicialTreasuryBalance() / GwpTuning.Firesworn.DamnationCostPerHead);
            Dictionary<CharacterObject, int> produced = new Dictionary<CharacterObject, int>();
            int prisonersBurned = _dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon)
                ? BurnPairs(dungeon, dungeon.GetTroopRoster(), budget, produced)
                : 0;
            budget -= prisonersBurned;
            int militiaBurned = 0;
            int householdsBurned = 0;
            if (IsPriestStrongest(row, village))
            {
                MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
                int militiaLimit = GwpTuning.Firesworn.DamnationMilitiaBase +
                                   (int)(village.Village.Hearth / GwpTuning.Firesworn.DamnationHearthPerMilitia);
                if (militia?.IsActive == true)
                    militiaBurned = BurnPairs(militia.MemberRoster,
                        militia.MemberRoster.GetTroopRoster().Where(element => !IsGuardTroop(element.Character)),
                        Math.Min(budget, militiaLimit), produced);
                budget -= militiaBurned;
                // 以前没扣烧俘虏感化的户、也没算烧掉的那户本身，户数减了以后信众户被压到等于户数，一场就 100%
                // （实机诊断 2026-10-08：烧 226 人、60 户，信众户 181→249 = 户数，日志里立誓户 −46）。
                int unbelievers = Math.Max(0, HouseholdCap(village) - row.Households - produced.Values.Sum());
                householdsBurned = Math.Min(budget,
                    Math.Min(unbelievers / 2, row.Households / GwpTuning.Firesworn.DamnationSwornPerBurnedHousehold));
            }
            if (prisonersBurned + militiaBurned + householdsBurned <= 0)
                return;

            int cost = (prisonersBurned + militiaBurned + householdsBurned) * GwpTuning.Firesworn.DamnationCostPerHead;
            PoliceResourceManager.SpendJudicialTreasury(cost);
            double now = CampaignTime.Now.ToHours;
            row.LastDamnation = now;
            int before = row.Households;
            float hearthBefore = village.Village.Hearth;
            // 烧死一个，另一个信教加入守村队，一户不信教的村民被感化（用户裁定 2026-10-07）。总户数不变；
            // 生出来的兵可以超过守村队上限（用户裁定 2026-10-08）。
            int risen = 0;
            foreach (KeyValuePair<CharacterObject, int> pair in produced)
            {
                row.Households = Math.Min(HouseholdCap(village), row.Households + pair.Value);
                risen += JoinGuard(row, village, pair.Key, pair.Value);
            }
            int householdsSworn = 0;
            if (householdsBurned > 0)
            {
                village.Village.Hearth -= householdsBurned;
                int sworn = row.Households;
                row.Households = Math.Min(HouseholdCap(village), row.Households + householdsBurned);
                householdsSworn = row.Households - sworn;
            }
            UpdatePriestPower(row);
            village.Party.SetVisualAsDirty();
            // 聚落上那一条只说烧了多少、多少户立誓（用户 2026-10-08：写得太多）；立誓户数连烧俘虏感化的一起算。
            List<string> burned = new List<string>();
            if (prisonersBurned + militiaBurned > 0)
                burned.Add(GwpText.Get("{=gwp_notice_burned_people}{COUNT} people", "COUNT", prisonersBurned + militiaBurned));
            if (householdsBurned > 0)
                burned.Add(GwpText.Get("{=gwp_notice_burned_homes}{COUNT} households", "COUNT", householdsBurned));
            GwpSettlementNotices.Post(village, FindLivingPriest(row),
                GwpText.Get("{=gwp_notice_damnation}{BURNED} went to the pyre; {SWORN} households swore",
                    "BURNED", string.Join(GwpText.Get("{=gwp_fs_party_products_separator}, "), burned),
                    "SWORN", row.Households - before));
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Damnation,
                EndHour = now + GwpTuning.Firesworn.DamnationLockHours
            });
            GwpAiDiagnostics.WriteFaith("DAMNATION",
                "village=" + village.StringId + "; prisonersBurned=" + prisonersBurned + "; militiaBurned=" + militiaBurned +
                "; householdsBurned=" + householdsBurned + "; householdsSworn=" + householdsSworn + "; cost=" + cost +
                "; produced=" + string.Join(",", produced.Select(pair => pair.Key.StringId + ":" + pair.Value)) +
                "; addedToGuard=" + risen + "; households=" + before + "->" + row.Households +
                "; hearth=" + hearthBefore.ToString("0") + "->" + village.Village.Hearth.ToString("0") + "; guard=" + Sworn(row) +
                "; dungeonLeft=" + (dungeon?.TotalManCount ?? 0));
        }

        /// <summary>从名单里成对烧（PickPairs），把烧掉的人从名单里拿掉，生成的灰袍兵记进 produced。返回烧了几个。</summary>
        private static int BurnPairs(TroopRoster roster, IEnumerable<TroopRosterElement> pool, int limit,
            Dictionary<CharacterObject, int> produced)
        {
            Dictionary<CharacterObject, int> burned = new Dictionary<CharacterObject, int>();
            foreach (KeyValuePair<CharacterObject, int> pair in PickPairs(pool.ToList(), limit, burned))
            {
                produced.TryGetValue(pair.Key, out int already);
                produced[pair.Key] = already + pair.Value;
            }
            foreach (KeyValuePair<CharacterObject, int> pair in burned)
            {
                int index = roster.FindIndexOfTroop(pair.Key);
                int healthy = roster.GetElementNumber(index) - roster.GetElementWoundedNumber(index);
                roster.AddToCounts(pair.Key, -pair.Value, false, -Math.Max(0, pair.Value - healthy));
            }
            return burned.Values.Sum();
        }

        /// <summary>仪式画面里主持人的名字：祭司，没有祭司就写村名。</summary>
        private string RitualHostName(Settlement village)
        {
            Hero? priest = _villages.TryGetValue(village.StringId, out FaithVillage? row) ? FindLivingPriest(row) : null;
            return priest?.Name?.ToString() ?? village.Name.ToString();
        }
    }
}
