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
    /// 按阶从高到低给所有灰袍兵配装；高阶只补空位，狂信者/新兵可换强装。
    ///
    /// 账本记「物品 + 品质 → 数量」，兵身上穿的也算在里面；配装是按现有兵力每小时重新分配算出来的
    /// （<see cref="ArmoryPlan"/>，不存档，随时可由账本和兵力推出）。所以我方守住战场时兵死了装备自然还在；
    /// 我方输了，留在战场的逐人库装照原版尸体缴获给胜方一部分（<see cref="SettleBattleLosses"/>）。
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

        // 每小时核对账本版本、兵力和编制；变化才重算，读档后按需现算。
        private readonly Dictionary<string, ArmoryPlan> _plans =
            new Dictionary<string, ArmoryPlan>(StringComparer.OrdinalIgnoreCase);

        // 一场实战里，每个持有者每个兵种已经发出去几套。只在这一场战斗里有意义，不存档（战斗中不能存档）。
        private readonly Dictionary<string, int> _issuedThisMission = new Dictionary<string, int>(StringComparer.Ordinal);
        private Mission? _issuedMission;

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
            if (mobile.IsMainParty)
                return mobile.StringId;
            if (mobile.IsMilitia && HasFireswornGuard(mobile.HomeSettlement))
                return mobile.HomeSettlement.StringId;
            if (_faithParties.ContainsKey(mobile.StringId))
                return mobile.StringId;
            return mobile.IsLordParty && mobile.LeaderHero != null && GwpWardenRecruitment.IsWardenParty(mobile)
                ? BindLordArmory(mobile) : null;
        }

        private ArmoryPlan PlanOf(string holder)
        {
            // 战斗内沿用已分的套装，不因伤亡重新洗牌。战斗外每次按账本和名册复核（没变就命中缓存），
            // 原版升级、招兵改了名册后不用等整点。
            if (_plans.TryGetValue(holder, out ArmoryPlan? plan) && HolderInBattle(holder)) return plan;
            return BuildPlanFor(holder);
        }

        private bool HolderInBattle(string holder) =>
            (_villages.ContainsKey(holder) ? Settlement.Find(holder).MilitiaPartyComponent?.MobileParty : FindArmoryParty(holder))
            ?.MapEvent != null;

        internal static int ArmoryCapacity(MobileParty party) =>
            Math.Max(party.Party.PartySizeLimit, party.MemberRoster.TotalManCount);

        private ArmoryPlan BuildPlanFor(string holder)
        {
            ItemRoster ledger = ItemsOf(_armories, holder);
            if (_villages.TryGetValue(holder, out FaithVillage? row))
            {
                TroopRoster guard = GuardOf(holder);
                List<TroopRosterElement> troops = ArmoryTroops(guard);
                int positions = Math.Max(GuardCap(row), Sworn(row));
                return PlanForCounts(holder, ledger, troops, Math.Max(0, positions - troops.Sum(e => e.Number)));
            }
            MobileParty? party = FindArmoryParty(holder);
            if (party == null)
                return PlanForCounts(holder, ledger, Array.Empty<TroopRosterElement>(), CountItems(ledger));
            List<TroopRosterElement> members = ArmoryTroops(party.MemberRoster, IsFaithVillager(party));
            int reserve = _faithParties.ContainsKey(holder) ? 0 :
                Math.Max(0, ArmoryCapacity(party) - members.Sum(e => e.Number));
            return PlanForCounts(holder, ledger, members, reserve, IsFaithVillager(party));
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

        /// <summary>
        /// 骑在马上能不能用：至少有一种用法不要求下马，而且不是在马上装不了箭的弩。原版 item_usage_sets 里长弓、长枪、
        /// 架枪姿势等用法标了 requires_no_mount，引擎用 MBItem.GetItemUsageSetFlags 查；弩另有 CantReloadOnHorseback
        /// （C#反编译 v1.4.8 MBItem、MissionWeapon、WeaponFlags；Native item_usage_sets.xml）。
        /// </summary>
        private static bool UsableMounted(ItemObject item) =>
            item.WeaponComponent?.Weapons.Any(weapon =>
                !weapon.WeaponFlags.HasAnyFlag(WeaponFlags.CantReloadOnHorseback) &&
                (string.IsNullOrEmpty(weapon.ItemUsage) ||
                 (MBItem.GetItemUsageSetFlags(weapon.ItemUsage) & ItemObject.ItemUsageSetFlags.RequiresNoMount) == 0)) ?? true;

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

        // 灰袍兵种名单，按本局的 CharacterObject 建一次；新开或读档会换 behavior 实例，不跨局复用。
        private List<CharacterObject>? _armoryUsers;

        /// <summary>
        /// 军械库只收灰袍兵能用的兵器护甲（用户裁定 2026-10-16）：配装类别之内（<see cref="KitKind"/>），
        /// 且至少一种灰袍兵种的技能够得着。马、马具、披风、男女有一方不能用的、谁都够不着的都不进库。
        /// </summary>
        private bool IsArmoryItem(ItemObject item) =>
            KitKind(item) >= 0 && (_armoryUsers ??= CharacterObject.All.Where(IsGuardTroop).ToList())
                .Any(troop => CanUse(troop, item));

        /// <summary>
        /// 收东西的唯一入口：能进军械库的进 <paramref name="holder"/> 的军械库，其余进它自己的背包——
        /// 领主部队、无领主队是部队背包，村子是货仓（用户裁定 2026-10-16：进背包，照原版去卖）。返回是否进了军械库。
        /// </summary>
        private bool Store(string holder, EquipmentElement item, int amount)
        {
            bool armory = IsArmoryItem(item.Item);
            ItemRoster target = armory ? ItemsOf(_armories, holder)
                : _villages.ContainsKey(holder) ? ItemsOf(_goods, holder) : FindArmoryParty(holder)!.ItemRoster;
            target.AddToCounts(item, amount);
            if (armory)
                _plans.Remove(holder);
            return armory;
        }

        private static int TierOf(EquipmentElement element) => element.IsEmpty ? 0 : (int)element.Item.Tier + 1;

        private static void Tiers(IEnumerable<ArmoryKit> sets, out float weapon, out float armor)
        {
            List<ArmoryKit> list = sets.ToList();
            weapon = armor = 0f;
            if (list.Count == 0)
                return;
            weapon = (float)list.Average(kit => new[] { KitMain, KitThrow, KitRanged }.Max(kind => TierOf(kit[kind])));
            armor = (float)list.Average(kit =>
                (TierOf(kit[KitHead]) + TierOf(kit[KitBody]) + TierOf(kit[KitLeg]) + TierOf(kit[KitGloves])) / 4f);
        }

        private void RefreshArmoryPlans()
        {
            BindExistingLordArmories();
            foreach (string holder in _armories.Keys.ToList())
            {
                // 战斗使用同一份分配，人数因伤亡变化时不能中途重分或让库装被再次发出。
                if (HolderInBattle(holder) && _plans.ContainsKey(holder)) continue;
                _plans[holder] = BuildPlanFor(holder);
            }
        }

        /// <summary>
        /// 每天一次：留够全部编制的最好装备以后，多出来的进持有者的背包交给原版处理——村子进货仓，由卖货队拉去卖；
        /// 领主部队进部队背包，照原版进城卖（用户裁定 2026-10-16，取代 10-15 的「领主余量留库」：现在全编制都留了装备，
        /// 多出来的只是更差或没人用的）。村与村之间另由运械队调剂（<see cref="LoadBalanceCargo"/>）。正在打仗的不动。
        /// </summary>
        private void RebalanceArmories()
        {
            RefreshArmoryPlans();
            foreach (KeyValuePair<string, ArmoryPlan> pair in _plans.ToList())
            {
                if (pair.Value.Surplus.Count == 0 || HolderInBattle(pair.Key))
                    continue;
                bool village = _villages.ContainsKey(pair.Key);
                MobileParty? lord = village ? null : FindArmoryParty(pair.Key);
                if (!village && lord?.IsLordParty != true)
                    continue;
                if (lord?.IsMainParty == true && Hero.MainHero.IsPrisoner) continue;
                int value = pair.Value.Surplus.TotalValue;
                int moved = MoveAll(pair.Value.Surplus, ItemsOf(_armories, pair.Key),
                    village ? ItemsOf(_goods, pair.Key) : lord!.ItemRoster);
                GwpAiDiagnostics.WriteFaith(village ? "ARMORY_SURPLUS_TO_GOODS" : "ARMORY_SURPLUS_TO_BACKPACK",
                    "holder=" + pair.Key + "; items=" + moved + "; value=" + value);
            }
            RefreshArmoryPlans();
        }

        /// <summary>
        /// 无领主队出发前从家里的军械库拿最好的那几套（用户裁定 2026-10-07：无领主队都拿自己武器库里最好的再出发）：
        /// 按实际出队兵种重新配装，整套挪进这支队的账；已有装备只补缺额。
        /// </summary>
        private int TakeBestSets(string homeId, MobileParty party)
        {
            // 人已经从家里扣掉：按实际出队的各阶灰袍兵配装，不能再用留守名单的套数。
            // 常驻卖货队已携带的装备仍在自己的账里，只补这次尚未满足的配装。
            ItemRoster available = new ItemRoster();
            available.Add(ItemsOf(_armories, party.StringId));
            available.Add(ItemsOf(_armories, homeId));
            ArmoryPlan escort = BuildPlan(available, ArmoryTroops(party.MemberRoster, IsFaithVillager(party)), 0, IsFaithVillager(party));
            ItemRoster taken = new ItemRoster();
            foreach (ArmoryKit kit in escort.Sets)
                foreach (EquipmentElement item in kit.Items.Where(entry => !entry.IsEmpty))
                    taken.AddToCounts(item, 1);
            foreach (ItemRosterElement item in ItemsOf(_armories, party.StringId))
            {
                int owned = taken.Where(entry => entry.EquipmentElement.Equals(item.EquipmentElement)).Sum(entry => entry.Amount);
                if (owned > 0)
                    taken.AddToCounts(item.EquipmentElement, -Math.Min(owned, item.Amount));
            }
            int moved = MoveAll(taken, ItemsOf(_armories, homeId), ItemsOf(_armories, party.StringId));
            _plans.Remove(homeId);
            _plans.Remove(party.StringId);
            return moved;
        }

        private void PackUnusedPartyArmory(MobileParty party)
        {
            if (!_armories.ContainsKey(party.StringId)) return;
            ArmoryPlan plan = BuildPlanFor(party.StringId);
            ItemRoster unused = new ItemRoster();
            unused.Add(plan.Free);
            unused.Add(plan.Surplus);
            MoveAll(unused, ItemsOf(_armories, party.StringId), party.ItemRoster);
            _plans.Remove(party.StringId);
        }

        /// <summary>这座村各阶守村兵有几成持有原配或军械库提供的主武器/远程武器。</summary>
        private float ArmedShare(string villageId)
        {
            ArmoryPlan plan = PlanOf(villageId);
            return plan.Sets.Count == 0 ? 0f : plan.Sets.Count(kit => IsArmoryKitArmed(kit)) / (float)plan.Sets.Count;
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
                // 不能进库的留在车上，随后按到村的规矩交货仓。
                if (!IsArmoryItem(element.EquipmentElement.Item))
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
            ItemRoster ledger = ItemsOf(_armories, partyId);
            int moved = 0;
            // 余量里不该在库的（旧档留下的马之类）随捐赠进村货仓，能用的进村军械库。
            foreach (ItemRosterElement element in plan.Surplus.ToList())
            {
                ledger.AddToCounts(element.EquipmentElement, -element.Amount);
                if (Store(villageId, element.EquipmentElement, element.Amount))
                    moved += element.Amount;
            }
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
            if (men <= 0 || troop.IsHero || !_armories.ContainsKey(fromHolder))
                return 0;
            ArmoryPlan plan = BuildPlanFor(fromHolder);
            IEnumerable<ArmoryKit> sets = plan.Sets.Where(kit => kit.Troop == troop);
            ItemRoster taken = new ItemRoster();
            foreach (ArmoryKit kit in sets.Take(men))
                foreach (EquipmentElement item in kit.Items.Where(entry => !entry.IsEmpty))
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
        private readonly Dictionary<string, (int Bodies, int Items, int Value, int Packed)> _lootThisBattle =
            new Dictionary<string, (int, int, int, int)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 军械库的进项照原版缴获（用户裁定 2026-10-06：不是把俘虏全扒下来，和玩家一样，打赢了从尸体上缴获一些）。
        /// 原版 AI 赢家从尸体上只拿钱（7.25×等级² × 1/55），玩家才拿东西：每具尸体最多 RoundRandomized(玩家在胜方的贡献份额) 件，
        /// 每件由 BattleRewardModel.GetLootedItemFromTroop 按尸体兵种的一套装备、按价值随机挑，同一类只拿一件
        /// （C#反编译 v1.4.8 MapEvent.LootDefeatedPartyCasualties、LootCasualtyCharacter）。
        /// 灰袍领主部队、信众村民兵照玩家这条路拿，东西进各自的军械库，不再拿那点钱。返回 true 表示已经处理，原版跳过。
        /// </summary>
        internal static bool LootCasualtyIntoArmory(CharacterObject casualty, MapEventParty winner)
        {
            // 玩家自行从背包整理入库，战利品仍由原版交给玩家挑选。
            if (_instance == null || winner.Party == PartyBase.MainParty)
                return false;
            string? holder = _instance.HolderOf(winner.Party);
            if (holder == null)
                return false;
            int total = winner.Party.MapEventSide.Parties.Sum(party => party.ContributionToBattle);
            int tries = MBRandom.RoundRandomized(winner.ContributionToBattle / (float)Math.Max(1, total));
            BattleRewardModel model = Campaign.Current.Models.BattleRewardModel;
            float value = model.GetExpectedLootedItemValueFromCasualty(winner.Party.LeaderHero, casualty);
            List<ItemObject.ItemTypeEnum> taken = new List<ItemObject.ItemTypeEnum>();
            int items = 0, worth = 0, packed = 0;
            for (int i = 0; i < tries; i++)
            {
                EquipmentElement item = model.GetLootedItemFromTroop(casualty, value);
                if (item.Item == null || taken.Contains(item.Item.Type))
                    continue;
                taken.Add(item.Item.Type);
                if (!_instance.Store(holder, item, 1))
                    packed++;
                items++;
                worth += item.ItemValue;
            }
            _instance._lootThisBattle.TryGetValue(holder, out (int Bodies, int Items, int Value, int Packed) sum);
            _instance._lootThisBattle[holder] = (sum.Bodies + 1, sum.Items + items, sum.Value + worth, sum.Packed + packed);
            return true;
        }

        private void WriteBattleLoot(MapEvent mapEvent)
        {
            foreach (KeyValuePair<string, (int Bodies, int Items, int Value, int Packed)> pair in _lootThisBattle)
                GwpAiDiagnostics.WriteFaith("ARMORY_LOOT",
                    "holder=" + pair.Key + "; bodies=" + pair.Value.Bodies + "; items=" + pair.Value.Items +
                    "; toBackpack=" + pair.Value.Packed + "; value=" + pair.Value.Value + "; mapEvent=" + mapEvent.EventType);
            _lootThisBattle.Clear();
        }

        // ---------------------------------------------------------------- 输了、没了

        /// <summary>领主的余库跟人物保留；无领主队全灭后，余库33%缴获、其余损毁。</summary>
        private void OnPartyGone(MobileParty party, PartyBase? destroyer)
        {
            _plans.Remove(party.StringId);
            if (!_faithParties.TryGetValue(party.StringId, out FaithParty? record))
            {
                if (party.IsLordParty && party.LeaderHero != null && !party.IsMainParty
                    && GwpWardenRecruitment.IsWardenParty(party)) BindLordArmory(party);
                return;
            }
            _faithParties.Remove(party.StringId);
            _marketCargo.Remove(party.StringId);
            int lost = 0, looted = 0;
            if (_armories.TryGetValue(party.StringId, out ItemRoster? kits))
            {
                lost = CountItems(kits);
                foreach (ItemRosterElement entry in kits)
                    for (int i = 0; i < entry.Amount; i++)
                        if (destroyer != null && MBRandom.RandomFloat < GwpTuning.Firesworn.ArmoryLossLootShare)
                        {
                            destroyer.ItemRoster.AddToCounts(entry.EquipmentElement, 1);
                            looted++;
                        }
                _armories.Remove(party.StringId);
            }
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteFaith("FAITH_PARTY_LOST",
                "party=" + party.StringId + "; kind=" + record.Kind + "; home=" + record.HomeId + "; phase=" + record.Phase +
                "; target=" + record.TargetId + "; cargoValue=" + party.ItemRoster.TotalValue + "; gold=" + party.PartyTradeGold +
                "; kitsLeft=" + lost + "; looted=" + looted + "; destroyer=" + (destroyer?.Id ?? "-"));
#endif
        }

        // ---------------------------------------------------------------- 实战、自动结算

        /// <summary>这个兵在这场实战里分到的那一套；没有就返回 null（照兵种原样）。</summary>
        internal static Equipment? TakeKitForAgent(PartyBase? party, CharacterObject troop, int uniqueSeed = -1)
        {
            if (_instance == null || troop.IsHero)
                return null;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return null;
            if (_instance._issuedMission != Mission.Current)
            {
                _instance._issuedMission = Mission.Current;
                _instance._issuedThisMission.Clear();
            }
            ArmoryKit? kit;
            MapEventParty? battleParty = party?.MapEventSide?.Parties.FirstOrDefault(entry => entry.Party == party);
            if (battleParty != null)
            {
                _instance.BattleLoadoutFor(holder, battleParty).BySeed.TryGetValue(uniqueSeed, out kit);
                if (kit?.Troop != troop) return null;
            }
            else
            {
                ArmoryPlan plan = _instance.PlanOf(holder);
                string key = holder + "|" + troop.StringId;
                _instance._issuedThisMission.TryGetValue(key, out int issued);
                _instance._issuedThisMission[key] = issued + 1;
                kit = plan.Sets.Where(entry => entry.Troop == troop).Skip(issued).FirstOrDefault();
            }
            if (kit == null) return null;
            Equipment equipment = kit.Equipment.Clone();
            // 沿用原版/整套预设补丁的原配品质抽取；库装保留账本中实际物品的品质。
            for (int slot = 0; slot < (int)EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                if (kit.Slots.Contains(slot)) continue;
                EquipmentElement element = equipment[slot];
                ItemModifier? modifier = element.Item?.ItemComponent?.ItemModifierGroup?.GetRandomItemModifierLootScoreBased();
                if (modifier == null) continue;
                element.SetModifier(modifier);
                equipment[slot] = element;
            }
            return equipment;
        }

        /// <summary>
        /// 纯化下来的兵交出自己那套装备：兵种自带的第一套战斗装备，一兵一套。能进军械库的进库，
        /// 马、马具、披风等进部队背包（用户裁定 2026-10-16）。返回进军械库的件数。
        /// </summary>
        internal static int DepositTroopKits(MobileParty party, CharacterObject troop, int count)
        {
            string? holder = _instance?.HolderOf(party.Party);
            if (_instance == null || holder == null || count <= 0)
                return 0;
            Equipment kit = troop.FirstBattleEquipment;
            int items = 0;
            for (EquipmentIndex slot = EquipmentIndex.WeaponItemBeginSlot; slot < EquipmentIndex.NumEquipmentSetSlots; slot++)
            {
                EquipmentElement element = kit[slot];
                if (element.Item != null && _instance.Store(holder, element, count))
                    items += count;
            }
            return items;
        }

        internal static bool TryGetArmoryTiers(PartyBase? party, CharacterObject troop, out float weapon, out float armor)
        {
            weapon = armor = 0f;
            if (_instance == null || troop.IsHero)
                return false;
            string? holder = _instance.HolderOf(party);
            if (holder == null || !_instance._armories.ContainsKey(holder))
                return false;
            MapEventParty? battleParty = party?.MapEventSide?.Parties.FirstOrDefault(entry => entry.Party == party);
            ArmoryPlan plan = battleParty == null ? _instance.PlanOf(holder)
                : _instance.BattleLoadoutFor(holder, battleParty).Plan;
            if (plan.TroopTiers.TryGetValue(troop, out (float Weapon, float Armor) tiers))
                (weapon, armor) = tiers;
            return true;
        }

        // ---------------------------------------------------------------- 开发版看库房（GwpFireswornArmoryView）
#if GWP_DIAGNOSTICS
        internal static string ArmoryHolderTitle(string holder)
        {
            int items = _instance!._armories.TryGetValue(holder, out ItemRoster? ledger) ? CountItems(ledger) : 0;
            return _instance._villages.ContainsKey(holder)
                ? GwpText.Get(GwpTextCatalog.gwp_fs_armory_village,
                    "VILLAGE", Settlement.Find(holder).Name, "ITEMS", items)
                : GwpText.Get(GwpTextCatalog.gwp_fs_armory_party,
                    "PARTY", (object?)FindArmoryParty(holder)?.Name ?? Hero.Find(holder.Substring(LordArmoryPrefix.Length))!.Name, "ITEMS", items);
        }

        internal static ItemRoster ArmoryLedger(string holder) =>
            _instance!._armories.TryGetValue(holder, out ItemRoster? ledger) ? ledger : new ItemRoster();

        internal static string ArmorySummary(string holder)
        {
            if (!_instance!._armories.ContainsKey(holder))
                return GwpText.Get(GwpTextCatalog.gwp_fs_armory_empty);
            ArmoryPlan plan = _instance.PlanOf(holder);
            return GwpText.Get(GwpTextCatalog.gwp_fs_armory_summary,
                "MEN", plan.Sets.Count, "ARMED", plan.Sets.Count(kit => IsArmoryKitArmed(kit)),
                "FREE", CountItems(plan.Free), "SURPLUS", CountItems(plan.Surplus));
        }

        /// <summary>开发版村庄库房中的待售货仓；领主军械库不读取背包。</summary>
        internal static ItemRoster VillageGoodsLedger(string village) => ItemsOf(_instance!._goods, village);
#endif

        // ---------------------------------------------------------------- 每日监控

        private void WriteDailySummary()
        {
#if GWP_DIAGNOSTICS
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
                plan.TroopTiers.TryGetValue(CharacterObject.Find(GwpIds.ZealotId), out (float Weapon, float Armor) zealotTiers);
                float zealotWeapon = zealotTiers.Weapon, zealotArmor = zealotTiers.Armor;
                GwpAiDiagnostics.WriteFaith("DAILY_VILLAGE",
                    "village=" + row.VillageId + "; hearth=" + village.Village.Hearth.ToString("0") +
                    "; households=" + row.Households + "; guard=" + Sworn(row) + "; share=" + SwornShare(row, village).ToString("0.00") +
                    "; population=" + GuardPopulation(row) + "; cap=" + GuardCap(row) + "; headroom=" + GuardHeadroom(row) +
                    "; guard=" + Composition(GuardOf(row.VillageId)) +
                    "; guardXp=" + (militia?.IsActive == true ? string.Join(",", militia.MemberRoster.GetTroopRoster()
                        .Where(troop => IsGuardTroop(troop.Character) && troop.Xp > 0)
                        .Select(troop => troop.Character.StringId + ":" + troop.Xp)) : "") +
                    "; vanillaMilitia=" + (militia?.IsActive == true ? militia.MemberRoster.TotalManCount - Sworn(row) : 0) +
                    "; priest=" + (priest?.StringId ?? "-") +
                    "; priestPower=" + (priest?.Power.ToString("0") ?? "-") +
                    "; others=" + string.Join(",", OtherNotables(village, priest).Select(hero => hero.Occupation + ":" + hero.Power.ToString("0"))) +
                    "; strongest=" + IsPriestStrongest(row, village) +
                    "; priestSlots=" + (priest == null ? "-" : string.Join(",", priest.VolunteerTypes.Select(troop => troop?.StringId ?? "_"))) +
                    "; dungeon=" + (_dungeons.TryGetValue(row.VillageId, out TroopRoster? dungeon) ? dungeon.TotalManCount : 0) +
                    "; armoryItems=" + (_armories.TryGetValue(row.VillageId, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armoryKinds=" + (armory == null ? "-" : KindCounts(armory)) +
                    "; armed=" + plan.Sets.Count(kit => IsArmoryKitArmed(kit)) + "/" + plan.Sets.Count +
                    "; weaponTier=" + zealotWeapon.ToString("0.0") + "; armorTier=" + zealotArmor.ToString("0.0") +
                    "; goodsValue=" + (_goods.TryGetValue(row.VillageId, out ItemRoster? goods) ? goods.TotalValue : 0) +
                    "; parties=" + (parties.Length == 0 ? "-" : parties) +
                    "; daysSinceDamnation=" + DaysSince(row.LastDamnation) + "; daysSinceCongregation=" + DaysSince(row.LastCongregation) +
                    "; daysSinceMission=" + DaysSince(row.LastMission));
            }
            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties().Where(party => party.IsActive && party.IsLordParty).ToList())
            {
                string holder = HolderOf(lord.Party)!;
                ArmoryPlan? plan = _armories.ContainsKey(holder) ? PlanOf(holder) : null;
                GwpAiDiagnostics.WriteFaith("DAILY_LORD",
                    "party=" + lord.StringId + "; members=" + lord.Party.NumberOfAllMembers + "/" + lord.Party.PartySizeLimit +
                    "; troops=" + Composition(lord.MemberRoster) +
                    "; prisoners=" + CountRegularPrisoners(lord) + "/" + lord.Party.PrisonerSizeLimit +
                    "; armoryItems=" + (_armories.TryGetValue(holder, out ItemRoster? armory) ? CountItems(armory) : 0) +
                    "; armed=" + (plan == null ? "0/0" : plan.Sets.Count(kit => IsArmoryKitArmed(kit)) + "/" + plan.Sets.Count) +
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
        #endif
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
#if GWP_DIAGNOSTICS
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
                    now - record.PhaseHour > 72d)
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
                    !GwpWardenDuties.HoldsAny(lord, WardenDuty.Reconstruction, WardenDuty.Issue))
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
                    plan.Sets.Count(kit => IsArmoryKitArmed(kit)) < plan.Sets.Count / 10)
                    Flag("ARMORY_UNARMED", "village=" + row.VillageId + "; armed=" +
                        plan.Sets.Count(kit => IsArmoryKitArmed(kit)) + "/" + plan.Sets.Count +
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
        #endif
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
    /// 实战里给各阶灰袍兵使用对应完整原配与库装分配（用户裁定：一个兵一套装备）。
    /// 原版在 Mission.SpawnAgent 里按 AgentBuildData 的装备生成兵（C#反编译 v1.4.8 Mission.SpawnAgent：
    /// 有覆盖装备就用覆盖的，没有就按兵种随机一套）。
    /// 高阶只补原配空位；新兵/狂信者可以换强装。肩部印章在兵种原配里，军械库不发肩部和马。显式覆盖的场景装备由原调用方负责。
    /// </summary>
    [HarmonyPatch(typeof(Mission), nameof(Mission.SpawnAgent))]
    internal static class GwpArmorySpawnPatch
    {
        private static void Prefix(AgentBuildData agentBuildData)
        {
            if (Campaign.Current == null || agentBuildData.AgentCivilianEquipment ||
                agentBuildData.AgentCharacter is not CharacterObject troop)
                return;
            PartyBase? party = agentBuildData.AgentOrigin switch
            {
                PartyAgentOrigin origin => origin.Party,
                PartyGroupAgentOrigin origin => origin.Party,
                SimpleAgentOrigin origin => origin.Party,
                _ => null
            };
            // 明确覆盖的场景装备由原调用方负责；不拿另一套原配的空位计划去覆盖它。
            if (agentBuildData.AgentOverridenSpawnEquipment != null)
                return;
            Equipment? equipment = GwpFireswornBehavior.TakeKitForAgent(party, troop, agentBuildData.AgentOrigin.UniqueSeed);
            if (!troop.IsHero && GwpCommon.IsGreyWardenTroop(troop) && PoliceEnforcementBehavior.IsCitySearchParty(party))
                // 没有库装时按原版这一刻会做的随机挑一套（Mission.SpawnAgent，C#反编译 v1.4.8），再换上黑甲。
                equipment = PoliceResourceManager.WithCitySearchUniform(equipment ?? Equipment.GetRandomEquipmentElements(
                    troop, true, Equipment.EquipmentType.Battle, agentBuildData.AgentEquipmentSeed));
            if (equipment != null)
                agentBuildData.Equipment(equipment);
        }
    }

    /// <summary>
    /// 自动结算不看装备，只看兵种阶数（C#反编译 v1.4.8 DefaultCombatSimulationModel.SimulateHit：
    /// 伤害按双方兵种 GetTroopPower 之比算）。军械库也要在自动结算里算数（用户裁定）：
    /// 各阶灰袍兵按额外库装的兵器阶数加伤害，挨打按分到的护甲阶数减伤害。
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
