using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 军械库（用户裁定 2026-10-05，docs/state/firesworn-economy.md）：每支灰袍领主部队、每座信众村各一本账，
    /// 自动武装狂信者和灰袍新兵，一个兵一套。不配马（不武装成骑兵），肩部固定灰袍印章。
    ///
    /// 账本记「物品 + 品质 → 数量」，兵身上穿的也算在里面；配装是按现有兵力每小时重新分配算出来的
    /// （<see cref="ArmoryPlan"/>，不存档，随时可由账本和兵力推出）。所以我方守住战场时兵死了装备自然还在；
    /// 我方输了，死去的兵那一套照原版尸体缴获给胜方一部分（<see cref="SettleBattleLosses"/>）。
    ///
    /// 实战里在原版生成每个兵时换上分到的那一套（思路同开源模组 Dynamic Troop Equipment System：
    /// 前置补丁 Mission.SpawnAgent 改 AgentBuildData.Equipment）；自动结算不看装备，
    /// 所以另按分到的装备阶数给伤害加减（<see cref="GwpFaithCombatSimulationModel"/>）。
    /// </summary>
    public sealed partial class GwpFireswornBehavior
    {
        internal const int KitHead = 0, KitBody = 1, KitLeg = 2, KitGloves = 3, KitMain = 4, KitShield = 5, KitThrow = 6,
            KitRanged = 7, KitAmmo = 8;
        private const int KitSlots = 9;
        // 原版一个兵的武器格：Weapon0 到 Weapon3，共四格（NumPrimaryWeaponSlots）。第五格 ExtraWeaponSlot 是放旗帜的，
        // 以前按 NumAllWeaponSlots（5）算，拿满四件武器的兵还会再配一面盾、塞进旗帜格（C#反编译 v1.4.8 EquipmentIndex）。
        private const int WeaponSlots = (int)EquipmentIndex.NumPrimaryWeaponSlots;

        /// <summary>一个持有者此刻的配装结果。由账本和兵力推出，不存档。</summary>
        private sealed class ArmoryPlan
        {
            public readonly List<EquipmentElement[]> Sets = new List<EquipmentElement[]>();
            public int Recruits;
            // 没分出去的：前一段是给狂信者增长留的余量，余量以外是多余，可以捐、可以卖。
            public readonly ItemRoster Free = new ItemRoster();
            public readonly ItemRoster Surplus = new ItemRoster();
            public float RecruitWeaponTier, RecruitArmorTier, ZealotWeaponTier, ZealotArmorTier;
        }

        // 每小时由账本和兵力重算，读档后第一个小时前由 PlanOf 现算。
        private readonly Dictionary<string, ArmoryPlan> _plans =
            new Dictionary<string, ArmoryPlan>(StringComparer.OrdinalIgnoreCase);

        // 一场实战里，每个持有者每个兵种已经发出去几套。只在这一场战斗里有意义，不存档（战斗中不能存档）。
        private readonly Dictionary<string, int> _issuedThisMission = new Dictionary<string, int>(StringComparer.Ordinal);
        private Mission? _issuedMission;

        internal static bool IsArmedTroop(CharacterObject? troop) =>
            troop != null && (troop.StringId == GwpIds.ZealotId || troop.StringId == GwpIds.NewRecruitId);

        // ---------------------------------------------------------------- 持有者与配装

        /// <summary>
        /// 这支部队用哪本账：灰袍领主部队、火誓者的无领主队用自己的；有火誓者的村子的民兵（守村队在里面）用村子的。
        /// 无领主队出发时从家里的军械库拿最好的那几套装进自己的账（用户裁定 2026-10-07），路上打仗就穿这些。其余没有。
        /// </summary>
        private string? HolderOf(PartyBase? party)
        {
            MobileParty? mobile = party?.MobileParty;
            if (mobile == null)
                return null;
            if (mobile.IsMilitia && HasFireswornGuard(mobile.HomeSettlement))
                return mobile.HomeSettlement.StringId;
            if (_faithParties.ContainsKey(mobile.StringId))
                return mobile.StringId;
            return mobile.IsLordParty && GwpWardenRecruitment.IsWardenParty(mobile) ? mobile.StringId : null;
        }

        private ArmoryPlan PlanOf(string holder)
        {
            if (_plans.TryGetValue(holder, out ArmoryPlan? plan))
                return plan;
            plan = BuildPlanFor(holder);
            _plans[holder] = plan;
            return plan;
        }

        private ArmoryPlan BuildPlanFor(string holder)
        {
            ItemRoster ledger = ItemsOf(_armories, holder);
            if (_villages.TryGetValue(holder, out FaithVillage? row))
            {
                Settlement village = Settlement.Find(holder);
                // 村子的账武装守村队；出门的无领主队带着自己的账。
                TroopRoster guard = GuardOf(holder);
                int villageRecruits = CountOf(guard, GwpIds.NewRecruitId);
                int villageZealots = CountOf(guard, GwpIds.ZealotId);
                int headroom = Math.Max(0, GuardCap(row) - Sworn(row));
                int reserve = Math.Min(headroom, Math.Max(GwpTuning.Firesworn.ArmoryVillageReserveMinimum,
                    (int)(Sworn(row) * GwpTuning.Firesworn.ArmoryReserveShare)));
                return BuildPlan(ledger, villageRecruits, villageZealots, reserve);
            }
            MobileParty party = FindParty(holder)!;
            int recruits = CountOf(party.MemberRoster, GwpIds.NewRecruitId);
            int zealots = CountOf(party.MemberRoster, GwpIds.ZealotId);
            // 无领主队只带自己身上那几套，不留增长余量。
            int partyReserve = _faithParties.ContainsKey(holder)
                ? 0
                : (int)Math.Ceiling((recruits + zealots) * GwpTuning.Firesworn.ArmoryReserveShare);
            return BuildPlan(ledger, recruits, zealots, partyReserve);
        }

        private static int CountOf(TroopRoster roster, string troopId) =>
            roster.GetTroopRoster().Where(element => element.Character.StringId == troopId).Sum(element => element.Number);

        /// <summary>
        /// 每一类装备按数值强度（<see cref="Strength"/>）从强到弱排好，一个一个兵往下发（新兵排在狂信者前面，先挑）：
        /// 每一类拿自己技能够得着的（原版物品的 Difficulty 对 RelevantSkill，按领装备的那个兵自己的技能算）、比兵种自带的那件更强的最强那件；
        /// 不如自带的就不发，上战场穿自带的（用户裁定 2026-10-08：比数值强度；和自带的比，更强才换；每个兵拿自己用得了的最强那件）。
        /// 上战场时武器只有四格（<see cref="GwpArmorySpawnPatch"/>），主武器之后先发投掷，再发远程（要配上对应的弹药：弓配箭、弩配矢，
        /// 没有就不发，占两格），最后是盾（手里的主武器是单手的、格子还有空才发；自带的武器也占格子）（用户裁定 2026-10-08）。
        /// 新兵和狂信者都用不了的、马、马具、披风、男女有一方不能用的，都算多余卖掉。
        /// 分完剩下的先留够余量（最强的那些），再多的就是多余。（用户裁定 2026-10-05：不配骑兵，肩部是灰袍印章；2026-10-08：弓弩箭矢进军械库，能用就用，不能用就卖，马不行）
        /// </summary>
        private static ArmoryPlan BuildPlan(ItemRoster ledger, int recruits, int zealots, int reserve)
        {
            ArmoryPlan plan = new ArmoryPlan { Recruits = recruits };
            CharacterObject recruit = CharacterObject.Find(GwpIds.NewRecruitId);
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            List<EquipmentElement>[] buckets = Enumerable.Range(0, KitSlots).Select(_ => new List<EquipmentElement>()).ToArray();
            for (int i = 0; i < ledger.Count; i++)
            {
                ItemRosterElement element = ledger.GetElementCopyAtIndex(i);
                int kind = KitKind(element.EquipmentElement.Item);
                if (kind < 0 || !CanUse(recruit, element.EquipmentElement.Item) && !CanUse(zealot, element.EquipmentElement.Item))
                {
                    plan.Surplus.AddToCounts(element.EquipmentElement, element.Amount);
                    continue;
                }
                for (int n = 0; n < element.Amount; n++)
                    buckets[kind].Add(element.EquipmentElement);
            }
            foreach (List<EquipmentElement> bucket in buckets)
                bucket.Sort((x, y) => Strength(y).CompareTo(Strength(x)));
            EquipmentElement[] recruitOwn = OwnBest(recruit);
            EquipmentElement[] zealotOwn = OwnBest(zealot);

            int men = recruits + zealots;
            for (int i = 0; i < men; i++)
            {
                CharacterObject troop = i < recruits ? recruit : zealot;
                EquipmentElement[] own = i < recruits ? recruitOwn : zealotOwn;
                EquipmentElement[] kit = new EquipmentElement[KitSlots];
                foreach (int kind in new[] { KitHead, KitBody, KitLeg, KitGloves, KitMain, KitThrow })
                    kit[kind] = Take(buckets[kind], item => CanUse(troop, item.Item) && Stronger(item, own[kind]));
                EquipmentElement ranged = Take(buckets[KitRanged], item => CanUse(troop, item.Item) && Stronger(item, own[KitRanged]) &&
                    buckets[KitAmmo].Any(ammo => ammo.Item.ItemType == AmmoFor(item.Item)));
                if (!ranged.IsEmpty)
                {
                    kit[KitRanged] = ranged;
                    kit[KitAmmo] = Take(buckets[KitAmmo], ammo => ammo.Item.ItemType == AmmoFor(ranged.Item));
                }
                // 上战场时没分到的那一类穿自带的，所以算格子、看主武器都要把自带的算进去。
                EquipmentElement main = kit[KitMain].IsEmpty ? own[KitMain] : kit[KitMain];
                int weapons = new[] { KitMain, KitThrow, KitRanged, KitAmmo }
                    .Count(kind => !kit[kind].IsEmpty || !own[kind].IsEmpty && (kind != KitAmmo || kit[KitRanged].IsEmpty));
                if (weapons < WeaponSlots && !main.IsEmpty && main.Item.ItemType == ItemObject.ItemTypeEnum.OneHandedWeapon)
                    kit[KitShield] = Take(buckets[KitShield], item => CanUse(troop, item.Item) && Stronger(item, own[KitShield]));
                plan.Sets.Add(kit);
            }
            foreach (List<EquipmentElement> bucket in buckets)
                for (int i = 0; i < bucket.Count; i++)
                    (i < reserve ? plan.Free : plan.Surplus).AddToCounts(bucket[i], 1);

            Tiers(plan.Sets.Take(recruits), out plan.RecruitWeaponTier, out plan.RecruitArmorTier);
            Tiers(plan.Sets.Skip(recruits), out plan.ZealotWeaponTier, out plan.ZealotArmorTier);
            return plan;
        }

        /// <summary>比兵种自带的那件强（自带的没有这一类就算强）。</summary>
        private static bool Stronger(EquipmentElement candidate, EquipmentElement own) =>
            own.IsEmpty || Strength(candidate) > Strength(own);

        /// <summary>兵种自带的每一类里最强的那件：所有战斗装备套里挑，没有就空着。</summary>
        private static EquipmentElement[] OwnBest(CharacterObject troop)
        {
            EquipmentElement[] best = new EquipmentElement[KitSlots];
            foreach (Equipment set in troop.BattleEquipments)
                for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
                {
                    EquipmentElement element = set[slot];
                    int kind = element.IsEmpty ? -1 : KitKind(element.Item);
                    if (kind >= 0 && (best[kind].IsEmpty || Strength(element) > Strength(best[kind])))
                        best[kind] = element;
                }
            return best;
        }

        /// <summary>
        /// 一件装备的数值强度：原版 <c>ItemObject.Effectiveness</c>（C#反编译 v1.4.8 ItemObject.CalculateEffectiveness：护甲按头 34、身 42、
        /// 腿 12、手 12 加权；近战看挥砍、刺击的速度乘伤害，加操控、长度、重量；弓弩看射速乘伤害、精度；弹药、投掷看伤害、速度、数量；
        /// 盾看护甲、耐久；再乘武器类别系数）。原版只按物品本身算；有前缀时把护甲值、伤害、速度、操控、耐久、数量换成改过以后的，
        /// 按同一公式的比例折算（类别系数两边一样，约掉）。开源模组 Dynamic Troop Equipment System 也拿 Effectiveness 比装备，但不看前缀
        /// （用户裁定 2026-10-08：要比数值强度）。
        /// </summary>
        internal static float Strength(EquipmentElement element)
        {
            ItemObject item = element.Item;
            if (element.ItemModifier == null)
                return item.Effectiveness;
            float plain = StrengthCore(new EquipmentElement(item));
            return plain > 0f ? item.Effectiveness * StrengthCore(element) / plain : item.Effectiveness;
        }

        /// <summary>原版强度公式去掉武器类别系数的那部分，数值取改过前缀以后的。</summary>
        private static float StrengthCore(EquipmentElement e)
        {
            ItemObject item = e.Item;
            if (item.ArmorComponent != null)
                return (e.GetModifiedHeadArmor() * 34f + e.GetModifiedBodyArmor() * 42f + e.GetModifiedLegArmor() * 12f +
                        e.GetModifiedArmArmor() * 12f) * 0.03f;
            WeaponComponentData? w = item.WeaponComponent?.PrimaryWeapon;
            if (w == null)
                return 0f;
            float swingSpeed = e.GetModifiedSwingSpeedForUsage(0), swingDamage = e.GetModifiedSwingDamageForUsage(0);
            float thrustSpeed = e.GetModifiedThrustSpeedForUsage(0), thrustDamage = e.GetModifiedThrustDamageForUsage(0);
            float missileSpeed = e.GetModifiedMissileSpeedForUsage(0), missileDamage = e.GetModifiedMissileDamageForUsage(0);
            // 原版的 MaxDataValue：弓弩是 ammo_limit，弹药、投掷是堆叠数量，盾是耐久；前缀只改后两种。
            float data = w.IsConsumable ? e.GetModifiedStackCountForUsage(0) : w.IsShield ? e.GetModifiedMaximumHitPointsForUsage(0) : w.MaxDataValue;
            if (w.IsRangedWeapon)
                return w.IsConsumable
                    ? (missileDamage * missileSpeed * 1.775f + w.Accuracy * data * 25f + w.WeaponLength * 4f) * 0.006944f * data
                    : (missileSpeed * missileDamage * 1.75f + thrustSpeed * w.Accuracy * 0.3f) * 0.01f * data;
            if (w.IsMeleeWeapon)
            {
                float swing = swingSpeed * swingDamage * 0.01f, thrust = thrustSpeed * thrustDamage * 0.01f;
                float high = Math.Max(swing, thrust), low = Math.Min(swing, thrust);
                return high <= 0f ? 0f
                    : ((high + low * low / high) * 120f + e.GetModifiedHandlingForUsage(0) * 15f + w.WeaponLength * 20f + item.Weight * 5f) * 0.01f;
            }
            if (w.IsConsumable)
                return (missileDamage * 550f + missileSpeed * 15f + data * 60f) * 0.01f;
            if (w.IsShield)
                return (w.BodyArmor * 60f + thrustSpeed * 10f + data * 40f + w.WeaponLength * 20f) * 0.01f;
            return 0f;
        }

        /// <summary>
        /// 上战场时排武器四格：主武器、投掷、远程、弹药、盾，按这个先后放（用户裁定 2026-10-08）；哪一类分到了军械库的就用它，
        /// 没分到的用兵种这一套里自带的（配装时自带的更强就不分）；放满四格为止。一件武器都没分到就不动。
        /// </summary>
        internal static void ArrangeWeapons(EquipmentElement[] kit, Equipment equipment)
        {
            int[] order = { KitMain, KitThrow, KitRanged, KitAmmo, KitShield };
            if (order.All(kind => kit[kind].IsEmpty))
                return;
            List<EquipmentElement> own = new List<EquipmentElement>();
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumPrimaryWeaponSlots; slot++)
                if (!equipment[slot].IsEmpty)
                    own.Add(equipment[slot]);
            List<EquipmentElement> weapons = new List<EquipmentElement>();
            foreach (int kind in order)
            {
                if (!kit[kind].IsEmpty)
                    weapons.Add(kit[kind]);
                // 分到了远程就配它自己的弹药，自带的弹药不再放。
                else if (kind != KitAmmo || kit[KitRanged].IsEmpty)
                    weapons.AddRange(own.Where(element => KitKind(element.Item) == kind));
            }
            weapons.AddRange(own.Where(element => KitKind(element.Item) < 0));
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumPrimaryWeaponSlots; slot++)
            {
                int index = slot - EquipmentIndex.WeaponItemBeginSlot;
                equipment[slot] = index < weapons.Count ? weapons[index] : EquipmentElement.Invalid;
            }
        }

        /// <summary>从排好的一类里拿走第一件合适的；没有就空着。</summary>
        private static EquipmentElement Take(List<EquipmentElement> bucket, Func<EquipmentElement, bool> fits)
        {
            int index = bucket.FindIndex(item => fits(item));
            if (index < 0)
                return EquipmentElement.Invalid;
            EquipmentElement taken = bucket[index];
            bucket.RemoveAt(index);
            return taken;
        }

        /// <summary>这个兵用得了这件东西：原版的门槛是物品的 Difficulty 对它的 RelevantSkill。</summary>
        private static bool CanUse(CharacterObject troop, ItemObject item) =>
            item.RelevantSkill == null || item.Difficulty <= troop.GetSkillValue(item.RelevantSkill);

        private static ItemObject.ItemTypeEnum AmmoFor(ItemObject ranged) =>
            ranged.ItemType == ItemObject.ItemTypeEnum.Crossbow ? ItemObject.ItemTypeEnum.Bolts : ItemObject.ItemTypeEnum.Arrows;

        /// <summary>这件东西进军械库的哪一类；马、马具、披风和男女有一方不能用的不进（-1）。</summary>
        private static int KitKind(ItemObject item)
        {
            // 灰袍兵男女混编（GwpWardenTroopGenderPatch），只配男女都能用的。
            if (item.ItemFlags.HasAnyFlag(ItemFlags.NotUsableByFemale | ItemFlags.NotUsableByMale))
                return -1;
            return item.ItemType switch
            {
                ItemObject.ItemTypeEnum.HeadArmor => KitHead,
                ItemObject.ItemTypeEnum.BodyArmor => KitBody,
                ItemObject.ItemTypeEnum.LegArmor => KitLeg,
                ItemObject.ItemTypeEnum.HandArmor => KitGloves,
                ItemObject.ItemTypeEnum.OneHandedWeapon => KitMain,
                ItemObject.ItemTypeEnum.TwoHandedWeapon => KitMain,
                ItemObject.ItemTypeEnum.Polearm => KitMain,
                ItemObject.ItemTypeEnum.Shield => KitShield,
                ItemObject.ItemTypeEnum.Thrown => KitThrow,
                ItemObject.ItemTypeEnum.Bow => KitRanged,
                ItemObject.ItemTypeEnum.Crossbow => KitRanged,
                ItemObject.ItemTypeEnum.Arrows => KitAmmo,
                ItemObject.ItemTypeEnum.Bolts => KitAmmo,
                _ => -1
            };
        }

        private static int TierOf(EquipmentElement element) => element.IsEmpty ? 0 : (int)element.Item.Tier + 1;

        private static void Tiers(IEnumerable<EquipmentElement[]> sets, out float weapon, out float armor)
        {
            List<EquipmentElement[]> list = sets.ToList();
            weapon = armor = 0f;
            if (list.Count == 0)
                return;
            weapon = (float)list.Average(kit => TierOf(kit[KitMain]));
            armor = (float)list.Average(kit =>
                (TierOf(kit[KitHead]) + TierOf(kit[KitBody]) + TierOf(kit[KitLeg]) + TierOf(kit[KitGloves])) / 4f);
        }

        private void RefreshArmoryPlans()
        {
            _plans.Clear();
            foreach (string holder in _armories.Keys.ToList())
                _plans[holder] = BuildPlanFor(holder);
        }

        /// <summary>
        /// 每天一次：村子军械库多出来的进货仓，由信众队拉去卖；领主部队多出来的进部队原版仓库，
        /// 到信众村时捐掉（用户裁定）。村与村之间的平衡由运械队把兵器运过去（<see cref="LoadBalanceCargo"/>）。
        /// </summary>
        private void RebalanceArmories()
        {
            RefreshArmoryPlans();
            foreach (KeyValuePair<string, ArmoryPlan> pair in _plans.ToList())
            {
                if (pair.Value.Surplus.Count == 0)
                    continue;
                bool village = _villages.ContainsKey(pair.Key);
                ItemRoster target = village ? ItemsOf(_goods, pair.Key) : FindParty(pair.Key)!.ItemRoster;
                int value = pair.Value.Surplus.TotalValue;
                int moved = MoveAll(pair.Value.Surplus, ItemsOf(_armories, pair.Key), target);
                GwpAiDiagnostics.WriteFaith(village ? "ARMORY_SURPLUS_TO_GOODS" : "ARMORY_SURPLUS_TO_INVENTORY",
                    "holder=" + pair.Key + "; items=" + moved + "; value=" + value);
            }
            RefreshArmoryPlans();
        }

        /// <summary>
        /// 无领主队出发前从家里的军械库拿最好的那几套（用户裁定 2026-10-07：无领主队都拿自己武器库里最好的再出发）：
        /// 家里的配装从好到差排好，前 <paramref name="men"/> 套整套挪进这支队的账。
        /// </summary>
        private int TakeBestSets(string homeId, MobileParty party)
        {
            int men = CountOf(party.MemberRoster, GwpIds.NewRecruitId) + CountOf(party.MemberRoster, GwpIds.ZealotId);
            ArmoryPlan home = BuildPlanFor(homeId);
            ItemRoster taken = new ItemRoster();
            foreach (EquipmentElement[] kit in home.Sets.Take(men))
                foreach (EquipmentElement item in kit.Where(entry => !entry.IsEmpty))
                    taken.AddToCounts(item, 1);
            int moved = MoveAll(taken, ItemsOf(_armories, homeId), ItemsOf(_armories, party.StringId));
            _plans.Remove(homeId);
            _plans.Remove(party.StringId);
            return moved;
        }

        /// <summary>
        /// 运械队装车（用户裁定 2026-10-07：兵器先进无领主队的背包，到了再进目标军械库，路上会被抢）。
        /// 押车的人已经带着自己那几套走了；按「家里少这些人、那边多这些人」算两边拿到主兵器的比例一样时
        /// 那边还差几套，从家里拿到兵器的人里配装最差的那几套整套装进背包。返回装了几套。
        /// </summary>
        private int LoadBalanceCargo(string homeId, string targetId, MobileParty party)
        {
            ArmoryPlan home = BuildPlanFor(homeId);
            ArmoryPlan target = BuildPlanFor(targetId);
            ArmoryPlan escort = BuildPlanFor(party.StringId);
            List<EquipmentElement[]> homeArmed = home.Sets.Where(kit => !kit[KitMain].IsEmpty).ToList();
            int escortArmed = escort.Sets.Count(kit => !kit[KitMain].IsEmpty);
            int targetArmed = target.Sets.Count(kit => !kit[KitMain].IsEmpty) + escortArmed;
            int targetMen = target.Sets.Count + escort.Sets.Count;
            int allMen = home.Sets.Count + targetMen;
            if (allMen <= 0)
                return 0;
            int wanted = (int)Math.Round((homeArmed.Count + targetArmed) * targetMen / (float)allMen);
            int sets = Math.Max(0, Math.Min(homeArmed.Count, wanted - targetArmed));
            ItemRoster cargo = new ItemRoster();
            foreach (EquipmentElement[] kit in homeArmed.Skip(homeArmed.Count - sets))
                foreach (EquipmentElement item in kit.Where(entry => !entry.IsEmpty))
                    cargo.AddToCounts(item, 1);
            MoveAll(cargo, ItemsOf(_armories, homeId), party.ItemRoster);
            _plans.Remove(homeId);
            return sets;
        }

        /// <summary>这座村的守村队里狂信者和新兵有几成拿到了主兵器。</summary>
        private float ArmedShare(string villageId)
        {
            ArmoryPlan plan = PlanOf(villageId);
            return plan.Sets.Count == 0 ? 0f : plan.Sets.Count(kit => !kit[KitMain].IsEmpty) / (float)plan.Sets.Count;
        }

        /// <summary>把一支无领主队自己的账整本并进 <paramref name="toHolder"/>，这支队的账就没了。</summary>
        private int ReturnPartyLedger(MobileParty party, string toHolder)
        {
            if (!_armories.TryGetValue(party.StringId, out ItemRoster? ledger))
                return 0;
            ItemRoster all = new ItemRoster();
            all.Add(ledger);
            int moved = MoveAll(all, ledger, ItemsOf(_armories, toHolder));
            _armories.Remove(party.StringId);
            _plans.Remove(party.StringId);
            _plans.Remove(toHolder);
            return moved;
        }

        /// <summary>无领主队把背包里的兵器护甲卸进这座村的军械库。</summary>
        private int UnloadKits(MobileParty cart, string village)
        {
            ItemRoster ledger = ItemsOf(_armories, village);
            int moved = 0;
            for (int i = cart.ItemRoster.Count - 1; i >= 0; i--)
            {
                ItemRosterElement element = cart.ItemRoster.GetElementCopyAtIndex(i);
                if (KitKind(element.EquipmentElement.Item) < 0)
                    continue;
                ledger.AddToCounts(element.EquipmentElement, element.Amount);
                cart.ItemRoster.AddToCounts(element.EquipmentElement, -element.Amount);
                moved += element.Amount;
            }
            _plans.Remove(village);
            return moved;
        }

        /// <summary>把 <paramref name="what"/> 里列的东西从账本 <paramref name="from"/> 挪到 <paramref name="to"/>。</summary>
        private static int MoveAll(ItemRoster what, ItemRoster from, ItemRoster to)
        {
            int moved = 0;
            for (int i = 0; i < what.Count; i++)
            {
                ItemRosterElement element = what.GetElementCopyAtIndex(i);
                from.AddToCounts(element.EquipmentElement, -element.Amount);
                to.AddToCounts(element.EquipmentElement, element.Amount);
                moved += element.Amount;
            }
            return moved;
        }

        /// <summary>领主进信众村时，部队军械库里余量以外多出来的捐给村军械库。</summary>
        private int DonateArmorySurplus(string partyId, string villageId)
        {
            if (!_armories.ContainsKey(partyId))
                return 0;
            ArmoryPlan plan = BuildPlanFor(partyId);
            int moved = MoveAll(plan.Surplus, ItemsOf(_armories, partyId), ItemsOf(_armories, villageId));
            _plans.Remove(partyId);
            _plans.Remove(villageId);
            return moved;
        }

        /// <summary>
        /// 从守村队招走的人带走他那一套（用户裁定：招兵连装备带走）：祭司格子拉的是配装最好的（用户裁定 2026-10-07），
        /// 这个兵种分到的套装按好坏排好，招走几个人就把最好的几套整套挪进部队军械库。
        /// </summary>
        private int MoveBestSets(string fromHolder, string toHolder, CharacterObject troop, int men)
        {
            if (men <= 0 || !IsArmedTroop(troop) || !_armories.ContainsKey(fromHolder))
                return 0;
            ArmoryPlan plan = BuildPlanFor(fromHolder);
            IEnumerable<EquipmentElement[]> sets = troop.StringId == GwpIds.NewRecruitId
                ? plan.Sets.Take(plan.Recruits)
                : plan.Sets.Skip(plan.Recruits);
            ItemRoster taken = new ItemRoster();
            foreach (EquipmentElement[] kit in sets.Take(men))
                foreach (EquipmentElement item in kit.Where(entry => !entry.IsEmpty))
                    taken.AddToCounts(item, 1);
            int moved = MoveAll(taken, ItemsOf(_armories, fromHolder), ItemsOf(_armories, toHolder));
            _plans.Remove(fromHolder);
            _plans.Remove(toHolder);
            if (moved > 0)
                GwpAiDiagnostics.WriteFaith("KITS_MOVED",
                    "from=" + fromHolder + "; to=" + toHolder + "; troop=" + troop.StringId + "; men=" + men + "; items=" + moved);
            return moved;
        }

        // ---------------------------------------------------------------- 缴获

        // 这一场战斗每本账缴获了几具尸体、几件东西。战斗结束时写一行监控就清掉；只给监控用，不存档。
        private readonly Dictionary<string, (int Bodies, int Items, int Value)> _lootThisBattle =
            new Dictionary<string, (int, int, int)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 军械库的进项照原版缴获（用户裁定 2026-10-06：不是把俘虏全扒下来，和玩家一样，打赢了从尸体上缴获一些）。
        /// 原版 AI 赢家从尸体上只拿钱（7.25×等级² × 1/55），玩家才拿东西：每具尸体最多 RoundRandomized(玩家在胜方的贡献份额) 件，
        /// 每件由 BattleRewardModel.GetLootedItemFromTroop 按尸体兵种的一套装备、按价值随机挑，同一类只拿一件
        /// （C#反编译 v1.4.8 MapEvent.LootDefeatedPartyCasualties、LootCasualtyCharacter）。
        /// 灰袍领主部队、信众村民兵照玩家这条路拿，东西进各自的军械库，不再拿那点钱。返回 true 表示已经处理，原版跳过。
        /// </summary>
        internal static bool LootCasualtyIntoArmory(CharacterObject casualty, MapEventParty winner)
        {
            if (_instance == null)
                return false;
            string? holder = _instance.HolderOf(winner.Party);
            if (holder == null)
                return false;
            int total = winner.Party.MapEventSide.Parties.Sum(party => party.ContributionToBattle);
            int tries = MBRandom.RoundRandomized(winner.ContributionToBattle / (float)Math.Max(1, total));
            BattleRewardModel model = Campaign.Current.Models.BattleRewardModel;
            float value = model.GetExpectedLootedItemValueFromCasualty(winner.Party.LeaderHero, casualty);
            ItemRoster ledger = ItemsOf(_instance._armories, holder);
            List<ItemObject.ItemTypeEnum> taken = new List<ItemObject.ItemTypeEnum>();
            int items = 0, worth = 0;
            for (int i = 0; i < tries; i++)
            {
                EquipmentElement item = model.GetLootedItemFromTroop(casualty, value);
                if (item.Item == null || taken.Contains(item.Item.Type))
                    continue;
                taken.Add(item.Item.Type);
                ledger.AddToCounts(item, 1);
                items++;
                worth += item.ItemValue;
            }
            _instance._lootThisBattle.TryGetValue(holder, out (int Bodies, int Items, int Value) sum);
            _instance._lootThisBattle[holder] = (sum.Bodies + 1, sum.Items + items, sum.Value + worth);
            _instance._plans.Remove(holder);
            return true;
        }

        private void WriteBattleLoot(MapEvent mapEvent)
        {
            foreach (KeyValuePair<string, (int Bodies, int Items, int Value)> pair in _lootThisBattle)
                GwpAiDiagnostics.WriteFaith("ARMORY_LOOT",
                    "holder=" + pair.Key + "; bodies=" + pair.Value.Bodies + "; items=" + pair.Value.Items +
                    "; value=" + pair.Value.Value + "; mapEvent=" + mapEvent.EventType);
            _lootThisBattle.Clear();
        }

        // ---------------------------------------------------------------- 输了、没了

        /// <summary>
        /// 我方输了：死去的狂信者、新兵身上那一套从账本里去掉，其中一部分归胜方——玩家在胜方就进玩家背包，
        /// 否则进胜方领头部队的背包（用户裁定：照原版尸体缴获，AI 抢走一部分）。我方守住战场时什么都不丢。
        /// 灰袍领主部队被打没了不走这里：它的账本在销毁时已经整本送回信众村（<see cref="OnPartyGone"/>）。
        /// </summary>
        private void SettleBattleLosses(MapEvent mapEvent)
        {
            if (mapEvent.BattleState != BattleState.AttackerVictory && mapEvent.BattleState != BattleState.DefenderVictory)
                return;
            MapEventSide loser = mapEvent.GetMapEventSide(mapEvent.DefeatedSide);
            MapEventSide winner = mapEvent.GetMapEventSide(mapEvent.WinningSide);
            ItemRoster? looter = winner.Parties.Any(entry => entry.Party == PartyBase.MainParty)
                ? PartyBase.MainParty.ItemRoster
                : winner.LeaderParty?.ItemRoster;
            foreach (MapEventParty party in loser.Parties)
            {
                string? holder = HolderOf(party.Party);
                if (holder == null || !_armories.ContainsKey(holder))
                    continue;
                int dead = 0;
                for (int i = 0; i < party.DiedInBattle.Count; i++)
                    if (IsArmedTroop(party.DiedInBattle.GetCharacterAtIndex(i)))
                        dead += party.DiedInBattle.GetElementNumber(i);
                if (dead <= 0)
                    continue;

                ArmoryPlan plan = PlanOf(holder);
                ItemRoster ledger = ItemsOf(_armories, holder);
                int lost = 0, looted = 0;
                foreach (EquipmentElement[] kit in plan.Sets.OrderBy(_ => MBRandom.RandomFloat).Take(dead))
                    foreach (EquipmentElement item in kit.Where(entry => !entry.IsEmpty))
                    {
                        ledger.AddToCounts(item, -1);
                        lost++;
                        if (looter != null && MBRandom.RandomFloat < GwpTuning.Firesworn.ArmoryLossLootShare)
                        {
                            looter.AddToCounts(item, 1);
                            looted++;
                        }
                    }
                _plans.Remove(holder);
                GwpAiDiagnostics.WriteFaith("ARMORY_LOST_IN_DEFEAT",
                    "holder=" + holder + "; dead=" + dead + "; items=" + lost + "; lootedByWinner=" + looted +
                    "; winner=" + (winner.LeaderParty?.Name?.ToString() ?? "-") + "; mapEvent=" + mapEvent.EventType);
            }
        }

        /// <summary>
        /// 部队没了：灰袍领主部队的军械库整本回到最近的信众村（用户裁定）。
        /// 火誓者的无领主队被打掉，身上剩下的那几套（被俘的人的）归打掉它的那一方，路上遗失（用户裁定 2026-10-07）；
        /// 背包里的货原版已经分给了胜方。并入村子时（<see cref="JoinVillage"/>）账已经先挪走了。
        /// </summary>
        private void OnPartyGone(MobileParty party, PartyBase? destroyer)
        {
            _plans.Remove(party.StringId);
            if (_faithParties.TryGetValue(party.StringId, out FaithParty? record))
            {
                _faithParties.Remove(party.StringId);
                int lost = 0;
                if (_armories.TryGetValue(party.StringId, out ItemRoster? kits))
                {
                    lost = CountItems(kits);
                    if (destroyer != null)
                        destroyer.ItemRoster.Add(kits);
                    _armories.Remove(party.StringId);
                }
                GwpAiDiagnostics.WriteFaith("FAITH_PARTY_LOST",
                    "party=" + party.StringId + "; kind=" + record.Kind + "; home=" + record.HomeId + "; phase=" + record.Phase +
                    "; target=" + record.TargetId + "; cargoValue=" + party.ItemRoster.TotalValue + "; gold=" + party.PartyTradeGold +
                    "; kitsLeft=" + lost + "; destroyer=" + (destroyer?.Id ?? "-"));
                return;
            }
            if (!_armories.TryGetValue(party.StringId, out ItemRoster? ledger))
                return;
            _armories.Remove(party.StringId);
            Settlement? village = FaithVillages()
                .OrderBy(settlement => settlement.GetPosition2D.Distance(party.GetPosition2D))
                .FirstOrDefault();
            if (village == null)
                return;
            ItemsOf(_armories, village.StringId).Add(ledger);
            _plans.Remove(village.StringId);
            GwpAiDiagnostics.WriteFaith("ARMORY_RELOCATED",
                "party=" + party.StringId + "; village=" + village.StringId + "; items=" + CountItems(ledger));
        }

        // ---------------------------------------------------------------- 实战、自动结算

        /// <summary>这个兵在这场实战里分到的那一套；没有就返回 null（照兵种原样）。</summary>
        internal static EquipmentElement[]? TakeKitForAgent(PartyBase? party, CharacterObject troop)
        {
            if (_instance == null || !IsArmedTroop(troop))
                return null;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return null;
            if (_instance._issuedMission != Mission.Current)
            {
                _instance._issuedMission = Mission.Current;
                _instance._issuedThisMission.Clear();
            }
            ArmoryPlan plan = _instance.PlanOf(holder);
            string key = holder + "|" + troop.StringId;
            _instance._issuedThisMission.TryGetValue(key, out int issued);
            _instance._issuedThisMission[key] = issued + 1;
            bool recruit = troop.StringId == GwpIds.NewRecruitId;
            int index = (recruit ? 0 : plan.Recruits) + issued;
            int end = recruit ? plan.Recruits : plan.Sets.Count;
            return index < end ? plan.Sets[index] : null;
        }

        /// <summary>自动结算里这一方这个兵种分到的装备平均阶数（兵器、护甲）。</summary>
        internal static bool TryGetArmoryTiers(PartyBase? party, CharacterObject troop, out float weapon, out float armor)
        {
            weapon = armor = 0f;
            if (_instance == null || !IsArmedTroop(troop))
                return false;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return false;
            ArmoryPlan plan = _instance.PlanOf(holder);
            bool recruit = troop.StringId == GwpIds.NewRecruitId;
            weapon = recruit ? plan.RecruitWeaponTier : plan.ZealotWeaponTier;
            armor = recruit ? plan.RecruitArmorTier : plan.ZealotArmorTier;
            return true;
        }

        // ---------------------------------------------------------------- 开发版看库房（GwpFireswornArmoryView）
#if GWP_DIAGNOSTICS
        internal static string ArmoryHolderTitle(string holder)
        {
            int items = _instance!._armories.TryGetValue(holder, out ItemRoster? ledger) ? CountItems(ledger) : 0;
            return _instance._villages.ContainsKey(holder)
                ? GwpText.Get("{=gwp_fs_armory_village}Armory of {VILLAGE} ({ITEMS} pieces)",
                    "VILLAGE", Settlement.Find(holder).Name, "ITEMS", items)
                : GwpText.Get("{=gwp_fs_armory_party}Armory of {PARTY} ({ITEMS} pieces)",
                    "PARTY", FindParty(holder)!.Name, "ITEMS", items);
        }

        internal static ItemRoster ArmoryLedger(string holder) =>
            _instance!._armories.TryGetValue(holder, out ItemRoster? ledger) ? ledger : new ItemRoster();

        internal static string ArmorySummary(string holder)
        {
            if (!_instance!._armories.ContainsKey(holder))
                return GwpText.Get("{=gwp_fs_armory_empty}Nothing here yet.");
            ArmoryPlan plan = _instance.PlanOf(holder);
            return GwpText.Get("{=gwp_fs_armory_summary}{MEN} of the sworn draw from this store; {ARMED} of them have a weapon from it. {FREE} pieces are kept back for those who will swear next. {SURPLUS} pieces nobody needs, and they will be sold.",
                "MEN", plan.Sets.Count, "ARMED", plan.Sets.Count(kit => !kit[KitMain].IsEmpty),
                "FREE", CountItems(plan.Free), "SURPLUS", CountItems(plan.Surplus));
        }

        /// <summary>库房里军械库以外的东西：信众村是货仓，灰袍领主部队是背包。</summary>
        internal static ItemRoster StoreLedger(string holder) =>
            _instance!._villages.ContainsKey(holder)
                ? ItemsOf(_instance._goods, holder)
                : FindParty(holder)?.ItemRoster ?? new ItemRoster();
#endif

        // ---------------------------------------------------------------- 每日监控

        private void WriteDailySummary()
        {
            WriteDailyAnomalies();
            foreach (FaithVillage row in _villages.Values)
            {
                Settlement village = Settlement.Find(row.VillageId);
                Hero? priest = FindLivingPriest(row);
                ArmoryPlan plan = PlanOf(row.VillageId);
                MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
                string parties = string.Join(",", _faithParties.Values
                    .Where(entry => string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase))
                    .Select(entry => entry.Kind + ":" + entry.Phase + ":" + FindParty(entry.PartyId)!.MemberRoster.TotalManCount));
                GwpAiDiagnostics.WriteFaith("DAILY_VILLAGE",
                    "village=" + row.VillageId + "; hearth=" + village.Village.Hearth.ToString("0") +
                    "; households=" + row.Households + "; guard=" + Sworn(row) + "; share=" + SwornShare(row, village).ToString("0.00") +
                    "; guard=" + Composition(GuardOf(row.VillageId)) +
                    "; vanillaMilitia=" + (militia?.IsActive == true ? militia.MemberRoster.TotalManCount - Sworn(row) : 0) +
                    "; priest=" + (priest?.StringId ?? "-") +
                    "; priestPower=" + (priest?.Power.ToString("0") ?? "-") +
                    "; othersPower=" + (priest == null ? "-" : string.Join(",", OtherNotables(village, priest).Select(hero => hero.Power.ToString("0")))) +
                    "; strongest=" + IsPriestStrongest(row, village) +
                    "; priestSlots=" + (priest == null ? "-" : string.Join(",", priest.VolunteerTypes.Select(troop => troop?.StringId ?? "_"))) +
                    "; dungeon=" + (_dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon) ? dungeon.TotalManCount : 0) +
                    "; armoryItems=" + (_armories.TryGetValue(row.VillageId, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armoryKinds=" + (armory == null ? "-" : KindCounts(armory)) +
                    "; armed=" + plan.Sets.Count(kit => !kit[KitMain].IsEmpty) + "/" + plan.Sets.Count +
                    "; weaponTier=" + plan.ZealotWeaponTier.ToString("0.0") + "; armorTier=" + plan.ZealotArmorTier.ToString("0.0") +
                    "; goodsValue=" + (_goods.TryGetValue(row.VillageId, out ItemRoster? goods) ? goods.TotalValue : 0) +
                    "; parties=" + (parties.Length == 0 ? "-" : parties) +
                    "; daysSinceDamnation=" + DaysSince(row.LastDamnation) + "; daysSinceCongregation=" + DaysSince(row.LastCongregation) +
                    "; daysSinceMission=" + DaysSince(row.LastMission));
            }
            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties().Where(party => party.IsActive && party.IsLordParty).ToList())
            {
                ArmoryPlan? plan = _armories.ContainsKey(lord.StringId) ? PlanOf(lord.StringId) : null;
                GwpAiDiagnostics.WriteFaith("DAILY_LORD",
                    "party=" + lord.StringId + "; members=" + lord.Party.NumberOfAllMembers + "/" + lord.Party.PartySizeLimit +
                    "; troops=" + Composition(lord.MemberRoster) +
                    "; prisoners=" + CountRegularPrisoners(lord) + "/" + lord.Party.PrisonerSizeLimit +
                    "; armoryItems=" + (_armories.TryGetValue(lord.StringId, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armed=" + (plan == null ? "0/0" : plan.Sets.Count(kit => !kit[KitMain].IsEmpty) + "/" + plan.Sets.Count) +
                    "; inventoryValue=" + NonFoodValue(lord.ItemRoster) +
                    "; faithVisit=" + GwpWardenVisitDesire.Describe(lord) +
                    "; case=" + CrimePool.HasTask(lord.StringId) + "; wage=" + lord.TotalWage +
                    "; partyGold=" + lord.PartyTradeGold);
            }
            GwpAiDiagnostics.WriteFaith("DAILY_TOTALS",
                "faithVillages=" + FaithVillages().Count() + "; swornVillages=" + _villages.Count +
                "; households=" + _villages.Values.Sum(row => row.Households) + "; guard=" + _villages.Values.Sum(Sworn) +
                "; strongestPriests=" + _villages.Values.Count(row => IsPriestStrongest(row, Settlement.Find(row.VillageId))) +
                "; dungeon=" + _dungeons.Values.Sum(roster => roster.TotalManCount) +
                "; treasury=" + PoliceResourceManager.GetJudicialTreasuryBalance() +
                "; activeParties=" + _faithParties.Values.Count(entry => entry.Phase != FaithPartyPhase.Home));
        }

        private static string Composition(TroopRoster roster) =>
            string.Join(",", roster.GetTroopRoster().Where(element => !element.Character.IsHero)
                .Select(element => element.Character.StringId + ":" + element.Number));

        /// <summary>军械库里每一类有几件（用户反馈 2026-10-07：要能看出库里是兵器多还是护甲多）。</summary>
        private static string KindCounts(ItemRoster ledger)
        {
            string[] names = { "head", "body", "leg", "gloves", "main", "shield", "throw", "ranged", "ammo" };
            int[] counts = new int[KitSlots + 1];
            for (int i = 0; i < ledger.Count; i++)
            {
                int kind = KitKind(ledger.GetElementCopyAtIndex(i).EquipmentElement.Item);
                counts[kind < 0 ? KitSlots : kind] += ledger.GetElementNumber(i);
            }
            return string.Join(",", names.Select((name, kind) => name + ":" + counts[kind])) + ",unusable:" + counts[KitSlots];
        }

        /// <summary>
        /// 每天主动找问题（用户 2026-10-06：监控要能看平衡、主动找问题）。每一条是一个 stage=ANOMALY 行，
        /// kind 说明是哪一类；tools/Analyze-Firesworn.py 按 kind 汇总。
        /// </summary>
        private void WriteDailyAnomalies()
        {
            double now = CampaignTime.Now.ToHours;
            void Flag(string kind, string details) => GwpAiDiagnostics.WriteFaith("ANOMALY", "kind=" + kind + "; " + details);

            foreach (FaithParty record in _faithParties.Values)
            {
                MobileParty party = FindParty(record.PartyId)!;
                Settlement home = Settlement.Find(record.HomeId);
                if (record.Phase == FaithPartyPhase.Home && party.CurrentSettlement != home)
                    Flag("FAITH_PARTY_ADRIFT", "party=" + party.StringId + "; home=" + home.StringId +
                        "; distance=" + party.GetPosition2D.Distance(home.GetPosition2D).ToString("0.0") +
                        "; default=" + party.DefaultBehavior);
                if (record.Phase != FaithPartyPhase.Home &&
                    now - record.PhaseHour > (record.Kind == FaithPartyKind.Patrol ? GwpTuning.Firesworn.PatrolHours : 0d) + 72d)
                    Flag("FAITH_PARTY_TRIP_LONG", "party=" + party.StringId + "; phase=" + record.Phase +
                        "; target=" + record.TargetId + "; hours=" + (now - record.PhaseHour).ToString("0"));
            }

            List<MobileParty> lords = PoliceStats.GetAllPoliceParties().Where(party => party.IsActive && party.IsLordParty).ToList();
            List<string> shortLords = lords
                .Where(lord => lord.Party.NumberOfAllMembers < lord.Party.PartySizeLimit * GwpTuning.Firesworn.AnomalyLowStrength)
                .Select(lord => lord.StringId + ":" + lord.Party.NumberOfAllMembers + "/" + lord.Party.PartySizeLimit)
                .ToList();
            foreach (MobileParty lord in lords)
            {
                // 赢的目标就是「正在这里」的聚落时原版原地待命；办重建、请愿、练兵的原地驻留不算。
                Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(lord);
                if (lord.DefaultBehavior == AiBehavior.Hold && lord.MapEvent == null && here != null &&
                    !GwpWardenDuties.HoldsAny(lord, WardenDuty.Reconstruction, WardenDuty.Issue, WardenDuty.Training) &&
                    !GreyWardenFamilyBehavior.IsTrainingParty(lord))
                    Flag("LORD_HOLDING_AT_SETTLEMENT", "party=" + lord.StringId + "; settlement=" + here.StringId +
                        "; inside=" + (lord.CurrentSettlement == here) + "; faithVisit=" + GwpWardenVisitDesire.Describe(lord));
                if (lord.Party.NumberOfAllMembers < lord.Party.PartySizeLimit * GwpTuning.Firesworn.AnomalyLowStrength)
                    Flag("LORD_UNDERSTRENGTH", "party=" + lord.StringId + "; members=" + lord.Party.NumberOfAllMembers +
                        "/" + lord.Party.PartySizeLimit + "; case=" + CrimePool.HasTask(lord.StringId) +
                        "; faithVisit=" + GwpWardenVisitDesire.Describe(lord));
            }

            foreach (FaithVillage row in _villages.Values)
            {
                Settlement village = Settlement.Find(row.VillageId);
                Hero? priest = FindLivingPriest(row);
                if (priest != null && priest.VolunteerTypes.All(troop => troop == null) && Sworn(row) > 0 && shortLords.Count > 0)
                    Flag("PRIEST_SLOTS_EMPTY", "village=" + row.VillageId + "; guard=" + Composition(GuardOf(row.VillageId)) +
                        "; shortLords=" + string.Join(",", shortLords));
                ArmoryPlan plan = PlanOf(row.VillageId);
                if (plan.Sets.Count > 0 && CountItems(ItemsOf(_armories, row.VillageId)) > plan.Sets.Count &&
                    plan.Sets.Count(kit => !kit[KitMain].IsEmpty) < plan.Sets.Count / 10)
                    Flag("ARMORY_UNARMED", "village=" + row.VillageId + "; armed=" +
                        plan.Sets.Count(kit => !kit[KitMain].IsEmpty) + "/" + plan.Sets.Count +
                        "; kinds=" + KindCounts(ItemsOf(_armories, row.VillageId)));
                if (_goods.TryGetValue(row.VillageId, out ItemRoster? goods) && goods.TotalValue > 50000)
                    Flag("GOODS_BACKLOG", "village=" + row.VillageId + "; goodsValue=" + goods.TotalValue +
                        "; town=" + village.Village.TradeBound?.StringId + "; townGold=" + village.Village.TradeBound?.Town.Gold);
            }

            // 用户反馈 2026-10-07「不同军械库之间数目不一」：账本里不该有数量不为正的条目，有就说明哪里扣多了。
            foreach (KeyValuePair<string, ItemRoster> pair in _armories)
                for (int i = 0; i < pair.Value.Count; i++)
                    if (pair.Value.GetElementNumber(i) <= 0)
                        Flag("ARMORY_NON_POSITIVE", "holder=" + pair.Key + "; item=" +
                            pair.Value.GetElementCopyAtIndex(i).EquipmentElement.Item.StringId +
                            "; amount=" + pair.Value.GetElementNumber(i));

            int treasury = PoliceResourceManager.GetJudicialTreasuryBalance();
            if (treasury < GwpTuning.Reconstruction.MinimumTreasuryReserve + GwpTuning.Reconstruction.MaximumCost)
                Flag("TREASURY_LOW", "treasury=" + treasury + "; reconstructionNeeds=" +
                    (GwpTuning.Reconstruction.MinimumTreasuryReserve + GwpTuning.Reconstruction.MinimumCost));
        }

        private static string DaysSince(double hour) =>
            hour <= Never ? "never" : ((CampaignTime.Now.ToHours - hour) / 24d).ToString("0.0");
    }

    [HarmonyPatch(typeof(MapEvent), "LootCasualtyCharacter")]
    internal static class GwpArmoryCasualtyLootPatch
    {
        private static bool Prefix(CharacterObject casualtyCharacter, MapEventParty winnerParty) =>
            !GwpFireswornBehavior.LootCasualtyIntoArmory(casualtyCharacter, winnerParty);
    }

    /// <summary>
    /// 实战里给狂信者、灰袍新兵换上军械库分到的那一套（用户裁定：一个兵一套装备）。
    /// 原版在 Mission.SpawnAgent 里按 AgentBuildData 的装备生成兵（C#反编译 v1.4.8 Mission.SpawnAgent：
    /// 有覆盖装备就用覆盖的，没有就按兵种随机一套）。
    /// 分到的护甲、兵器换上去，没分到的那一格照兵种原样（配装时不如自带的就不分）；肩部换成灰袍印章（用户裁定），不配马。
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
    internal static class GwpArmorySpawnPatch
    {
        private static void Prefix(AgentBuildData agentBuildData)
        {
            if (Campaign.Current == null || agentBuildData.AgentCivilianEquipment ||
                agentBuildData.AgentCharacter is not CharacterObject troop ||
                !GwpFireswornBehavior.IsArmedTroop(troop))
                return;
            PartyBase? party = agentBuildData.AgentOrigin switch
            {
                PartyAgentOrigin origin => origin.Party,
                PartyGroupAgentOrigin origin => origin.Party,
                SimpleAgentOrigin origin => origin.Party,
                _ => null
            };
            EquipmentElement[]? kit = GwpFireswornBehavior.TakeKitForAgent(party, troop);
            if (kit == null)
                return;

            Equipment equipment = agentBuildData.AgentOverridenSpawnEquipment?.Clone() ??
                                  Equipment.GetRandomEquipmentElements(troop, true, Equipment.EquipmentType.Battle,
                                      agentBuildData.AgentEquipmentSeed);
            EquipmentIndex[] armor = { EquipmentIndex.Head, EquipmentIndex.Body, EquipmentIndex.Leg, EquipmentIndex.Gloves };
            for (int i = 0; i < armor.Length; i++)
                if (!kit[i].IsEmpty)
                    equipment[armor[i]] = kit[i];
            GwpFireswornBehavior.ArrangeWeapons(kit, equipment);
            equipment[EquipmentIndex.Cape] = CharacterObject.Find(GwpIds.NewRecruitId).FirstBattleEquipment[EquipmentIndex.Cape];
            equipment[EquipmentIndex.Horse] = EquipmentElement.Invalid;
            equipment[EquipmentIndex.HorseHarness] = EquipmentElement.Invalid;
            agentBuildData.Equipment(equipment);
        }
    }

    /// <summary>
    /// 自动结算不看装备，只看兵种阶数（C#反编译 v1.4.8 DefaultCombatSimulationModel.SimulateHit：
    /// 伤害按双方兵种 GetTroopPower 之比算）。军械库也要在自动结算里算数（用户裁定）：
    /// 狂信者、新兵出手按分到的兵器阶数加伤害，挨打按分到的护甲阶数减伤害。
    /// </summary>
    public sealed class GwpFaithCombatSimulationModel : CombatSimulationModel
    {
        private CombatSimulationModel Inner => BaseModel;

        public override ExplainedNumber SimulateHit(CharacterObject strikerTroop, CharacterObject struckTroop,
            PartyBase strikerParty, PartyBase struckParty, float strikerAdvantage, MapEvent battle,
            float strikerSideMorale, float struckSideMorale)
        {
            ExplainedNumber damage = Inner.SimulateHit(strikerTroop, struckTroop, strikerParty, struckParty,
                strikerAdvantage, battle, strikerSideMorale, struckSideMorale);
            if (GwpFireswornBehavior.TryGetArmoryTiers(strikerParty, strikerTroop, out float weapon, out _))
                damage.AddFactor(weapon * GwpTuning.Firesworn.ArmoryWeaponTierDamage);
            if (GwpFireswornBehavior.TryGetArmoryTiers(struckParty, struckTroop, out _, out float armor))
                damage.AddFactor(1f / (1f + armor * GwpTuning.Firesworn.ArmoryArmorTierProtection) - 1f);
            return damage;
        }

        public override ExplainedNumber SimulateHit(Ship strikerShip, Ship struckShip, PartyBase strikerParty,
            PartyBase struckParty, SiegeEngineType siegeEngine, float strikerAdvantage, MapEvent battle,
            out int troopCasualties) =>
            Inner.SimulateHit(strikerShip, struckShip, strikerParty, struckParty, siegeEngine, strikerAdvantage,
                battle, out troopCasualties);

        public override (int defenderRounds, int attackerRounds) GetSimulationTicksForBattleRound(MapEvent mapEvent) =>
            Inner.GetSimulationTicksForBattleRound(mapEvent);

        public override int GetNumberOfEquipmentsBuilt(Settlement settlement) =>
            Inner.GetNumberOfEquipmentsBuilt(settlement);

        public override float GetMaximumSiegeEquipmentProgress(Settlement settlement) =>
            Inner.GetMaximumSiegeEquipmentProgress(settlement);

        public override float GetSettlementAdvantage(Settlement settlement) =>
            Inner.GetSettlementAdvantage(settlement);

        public override void GetBattleAdvantage(MapEvent mapEvent, out ExplainedNumber defenderAdvantage,
            out ExplainedNumber attackerAdvantage) =>
            Inner.GetBattleAdvantage(mapEvent, out defenderAdvantage, out attackerAdvantage);

        public override float GetShipSiegeEngineHitChance(Ship ship, SiegeEngineType siegeEngineType,
            BattleSideEnum battleSide) =>
            Inner.GetShipSiegeEngineHitChance(ship, siegeEngineType, battleSide);

        public override int GetPursuitRoundCount(MapEvent mapEvent) => Inner.GetPursuitRoundCount(mapEvent);

        public override float GetBluntDamageChance(CharacterObject strikerTroop, CharacterObject strikedTroop,
            PartyBase strikerParty, PartyBase strikedParty, MapEvent battle) =>
            Inner.GetBluntDamageChance(strikerTroop, strikedTroop, strikerParty, strikedParty, battle);

        public override CampaignTime GetSimulationTickInterval(MapEvent mapEvent) =>
            Inner.GetSimulationTickInterval(mapEvent);

        public override MBList<(Ship, MapEventParty)> GetSimulationShips(MapEvent mapEvent,
            MBList<MapEventParty> battleParties) =>
            Inner.GetSimulationShips(mapEvent, battleParties);

        public override int GetParticipatingTroopCount(MapEventSide side) => Inner.GetParticipatingTroopCount(side);
    }
}
