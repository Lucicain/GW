using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Naval;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Party.MobileParty;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 警察资源管理 + 兵员纯化
    /// 1. 常驻领主资源完全交给原版经济和城镇访问欲望：不发钱、不造粮、不免费补兵
    ///    无英雄的临时纠察/支援队是一次性任务单位，只在生成时携带固定行军口粮，
    ///    不参与灰袍家族收支，也不会进城购买补给。
    /// 2. 每6小时净化各警察部队兵种（原版招募的外族兵替换为灰袍新兵）
    /// 3. 保留旧补给 API 仅用于存档/调用兼容；它只请求重新思考，不下移动命令
    /// </summary>
    public class PoliceResourceManager : CampaignBehaviorBase
    {
        private const int EquipmentSlotCount = 12;
        private const int TroopsPerShip = 50;
        private const int TemporaryDutyFoodDays = 20;
        private const string ShipyardBuildingTypeId = "building_shipyard";
        internal const int SuccessfulCaseReward = 3000;
        // NavalDLC 可选依赖：运行时一次性检测（所有模块 DLL 加载后）
        // 若 NavalDLC 未安装，GivePoliceShips 直接 return，不影响游玩
        private static readonly bool _navalDlcLoaded =
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NavalDLC");

        // 每个部队的上次兵员净化时间（小时）
        private Dictionary<string, double> _lastPurifyTime =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private const double PurifyIntervalHours = 6.0;
        public static bool IsResupplying(MobileParty party) => false;

        public static bool IsReady(MobileParty party)
        {
            if (party == null || !party.IsActive) return false;
            return true;
        }

        public static void CancelResupply(MobileParty police) =>
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);

        public static void StartResupply(MobileParty police) =>
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);

        /// <summary>
        /// 临时纠察队和追截支援队没有英雄领队，原版不会让它们进城买粮；它们也
        /// 不是 WarPartyComponent，不会进入氏族军费结算。生成时明确清零独立钱袋，
        /// 并按原版每二十人每日一份粮食的基础消耗携带二十日口粮。剩余口粮随
        /// 一次性部队销毁，不进入灰袍家族库存或金库。
        /// </summary>
        internal static void ProvisionTemporaryDutyParty(MobileParty? party)
        {
            if (party?.IsActive != true) return;

            party.PartyTradeGold = 0;
            if (party.ItemRoster.TotalFood > 0) return;

            ItemObject? grain = MBObjectManager.Instance.GetObject<ItemObject>(GwpIds.GrainItemId);
            if (grain == null) return;

            int men = Math.Max(1, party.MemberRoster.TotalManCount);
            int grainCount = Math.Max(1,
                (int)Math.Ceiling(men / 20f * TemporaryDutyFoodDays));
            party.ItemRoster.AddToCounts(grain, grainCount);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
            CampaignEvents.OnShipOwnerChangedEvent.AddNonSerializedListener(this, OnShipOwnerChanged);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                _lastPurifyTime = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }

            List<string> keys = null!;
            List<double> values = null!;
            if (dataStore.IsSaving)
            {
                keys = new List<string>(_lastPurifyTime.Keys);
                values = new List<double>(_lastPurifyTime.Values);
            }
            dataStore.SyncData("GWPP_PurifyKeys", ref keys);
            dataStore.SyncData("GWPP_PurifyValues", ref values);
            if (dataStore.IsLoading && keys != null && values != null)
            {
                int count = Math.Min(keys.Count, values.Count);
                for (int i = 0; i < count; i++)
                    if (!string.IsNullOrEmpty(keys[i]))
                        _lastPurifyTime[keys[i]] = values[i];
            }
        }

        #region 每日资源与成年战斗员保障

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            CleanupLeaderlessPoliceLordParties();
            EnsureAllAdultGreyWardensAreCombatants();
            RepairGeneratedAdultCommanderLoadouts();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            CleanupLeaderlessPoliceLordParties();
            EnsureAllAdultGreyWardensAreCombatants();
            RepairGeneratedAdultCommanderLoadouts();
            // 旧存档中已存在的临时队伍可能由旧版本以零粮生成。只在完全无粮时
            // 补发一次，不因重复读档刷新仍未吃完的口粮。
            foreach (MobileParty party in MobileParty.All.Where(static party =>
                         party?.IsActive == true &&
                         (GwpCommon.IsPatrolParty(party) ||
                          GwpCommon.IsEnforcementDelayPatrolParty(party))).ToList())
            {
                ProvisionTemporaryDutyParty(party);
            }
        }

        private static void CleanupLeaderlessPoliceLordParties()
        {
            foreach (MobileParty party in MobileParty.All.Where(static party =>
                         party?.IsActive == true && party.IsLordParty &&
                         party.LeaderHero == null &&
                         string.Equals(party.ActualClan?.StringId, PoliceStats.PoliceClanId,
                             StringComparison.OrdinalIgnoreCase)).ToList())
            {
                GwpAiDiagnostics.WritePartyLifecycle(party,
                    "LEADERLESS_POLICE_LORD_PARTY_CLEANUP", string.Empty);
                try { DestroyPartyAction.Apply(null, party); } catch { }
            }
        }

        private void OnDailyTick()
        {
            // 防止警察家族被引擎标记为已消灭
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan?.IsEliminated == true)
            {
                typeof(Clan)
                    .GetField("_isEliminated", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(policeClan, false);
            }

            CollectDailyVillageProtectionContributions();
            SellSurplusPoliceShips();
            EnsureAllAdultGreyWardensAreCombatants();
        }

        /// <summary>
        /// 全大陆村庄按当前户数每日向司法公库缴纳保护费：每户 0.05 第纳尔。
        /// 户数只作为计费基数，不因缴费发生变化。
        /// </summary>
        private static void CollectDailyVillageProtectionContributions()
        {
            double exactContribution = 0d;

            foreach (Village? village in Village.All)
            {
                if (village == null) continue;

                float currentHearth = Math.Max(0f, village.Hearth);
                exactContribution += currentHearth * 0.05d;
            }

            int totalContribution = Math.Max(
                0,
                (int)Math.Floor(exactContribution));
            CreditJudicialTreasury(totalContribution);
        }

        private static void EnsureAllAdultGreyWardensAreCombatants()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return;

            foreach (Hero? hero in policeClan.Heroes.ToList())
            {
                EnsureAdultGreyWardenIsCombatant(hero);
            }
        }


        private void OnHeroComesOfAge(Hero hero)
        {
            if (hero == null || !IsPoliceClanHero(hero)) return;
            EnsureAdultGreyWardenIsCombatant(hero);
        }

        internal static bool IsPoliceClanHero(Hero? hero)
        {
            return hero?.Clan != null &&
                   string.Equals(hero.Clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 在原版成年换装完成后补上灰袍领主套装。原版
        /// AgingCampaignBehavior.OnHeroComesOfAge 会在自己的事件回调里重新生成
        /// 战斗/平民装备，因此不能只依赖另一个同级事件监听器。
        /// </summary>
        internal static bool EnsureCommanderLoadout(Hero? hero, string reason)
        {
            if (hero == null || !IsPoliceClanHero(hero) || hero.IsDead ||
                hero.IsChild || hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge)
                return false;

            CharacterObject? template = CharacterObject.Find(GwpIds.CommanderTemplateCharacterId)
                ?? hero.Clan?.Leader?.CharacterObject;
            if (template == null) return false;

            Equipment battleTemplate = template.FirstBattleEquipment;
            Equipment civilianTemplate = template.FirstCivilianEquipment;
            if (battleTemplate == null || battleTemplate.IsEmpty()) return false;
            if (civilianTemplate == null || civilianTemplate.IsEmpty()) return false;
            if (EquipmentMatches(battleTemplate, hero.BattleEquipment) &&
                EquipmentMatches(civilianTemplate, hero.CivilianEquipment))
                return false;

            // 先确保英雄拥有独立装备实例，避免写入共享的默认死者装备。
            hero.ResetEquipments();
            CopyEquipment(battleTemplate, hero.BattleEquipment);
            CopyEquipment(civilianTemplate, hero.CivilianEquipment);
            hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "ADULT_COMMANDER_LOADOUT_APPLIED",
                "reason=" + reason + "; template=" + template.StringId);
            return true;
        }

        private static void RepairGeneratedAdultCommanderLoadouts()
        {
            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null)
                return;

            foreach (Hero hero in policeClan.Heroes
                         .Where(GreyWardenFamilyBehavior.IsGeneratedPoliceHero)
                         .Where(hero => hero.IsAlive && !hero.IsChild &&
                                        hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge)
                         .ToList())
            {
                EnsureCommanderLoadout(hero, "existing_save_repair");
            }
        }

        private static bool EquipmentMatches(Equipment expected, Equipment actual)
        {
            if (expected == null || actual == null)
                return false;

            for (int i = 0; i < EquipmentSlotCount; i++)
            {
                EquipmentElement expectedElement = expected[i];
                EquipmentElement actualElement = actual[i];
                if (expectedElement.Item != actualElement.Item ||
                    expectedElement.ItemModifier != actualElement.ItemModifier)
                    return false;
            }

            return true;
        }

        private static void EnsureAdultGreyWardenIsCombatant(Hero? hero)
        {
            if (hero == null || !IsPoliceClanHero(hero) || hero.IsDead || hero.IsDisabled ||
                hero.IsChild || hero.Age < Campaign.Current.Models.AgeModel.HeroComesOfAge ||
                !hero.IsNoncombatant)
                return;

            // 正常情况下 PoliceHeroCreationModel 已直接返回战斗员。这里保留一次
            // 运行时兜底，防止其他模组在更晚阶段替换 HeroCreationModel。
            int oldValue = hero.GetSkillValue(DefaultSkills.OneHanded);
            hero.SetSkillValue(DefaultSkills.OneHanded, Math.Max(100, oldValue));
            GwpAiDiagnostics.WriteHeroLifecycle(hero, "ADULT_COMBATANT_INVARIANT_REPAIRED",
                "skill=OneHanded; old=" + oldValue + "; new=" +
                hero.GetSkillValue(DefaultSkills.OneHanded));
        }

        private static void CopyEquipment(Equipment source, Equipment destination)
        {
            if (source == null || destination == null) return;
            for (int i = 0; i < EquipmentSlotCount; i++)
                destination[i] = source[i];
        }


        #endregion

        #region 补给流程


        #endregion

        #region 兵员纯化（每6小时）

        private void OnHourlyTickParty(MobileParty party)
        {
            if (party == null || party.IsCaravan || party.IsMilitia || party.IsVillager) return;

            var clan = party.LeaderHero?.Clan;
            if (clan == null) return;
            if (!string.Equals(clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase)) return;

            // 领主队现在由原版自然创建，不再经过模组的 SpawnLordParty 入口；
            // 在首次小时维护补齐同样的航海载具，之后调用保持幂等。
            GivePoliceShips(party);

            double now = CampaignTime.Now.ToHours;
            if (_lastPurifyTime.TryGetValue(party.StringId, out double lastCheck) &&
                now - lastCheck < PurifyIntervalHours) return;

            _lastPurifyTime[party.StringId] = now;
            PurifyParty(party);
        }

        private void PurifyParty(MobileParty party)
        {
            var recruit = CharacterObject.Find(GwpIds.NewRecruitId);
            if (recruit == null) return;

            var roster = party.MemberRoster;
            var toRemove = new List<TroopRosterElement>();

            foreach (var element in roster.GetTroopRoster())
            {
                if (element.Character == null || element.Character.IsHero) continue;
                if (IsIllegalTroop(element.Character))
                    toRemove.Add(element);
            }

            foreach (var element in toRemove)
            {
                roster.AddToCounts(element.Character, -element.Number);
                roster.AddToCounts(recruit, element.Number);
            }
        }

        private static bool IsIllegalTroop(CharacterObject character)
        {
            return !GwpCommon.IsGreyWardenTroop(character);
        }

        #endregion

        #region 补兵 / 补粮 / 释放俘虏


        /// <summary>
        /// 按当前兵力为警察部队补足缺少的船只。
        /// 规则：每 50 人 1 艘，向上取整，最少 1 艘。
        /// 只追加缺失的船，不删除现有船，也不重建整个舰队。
        /// 不安装任何升级件，也不挂船首像。
        /// 无 NavalDLC 时静默跳过，不报错。
        /// </summary>
        internal static void GivePoliceShips(MobileParty party)
        {
            if (!_navalDlcLoaded) return;
            try
            {
                if (party == null || !party.IsActive || party.Party == null) return;

                int requiredCount = GetRequiredShipCount(party);
                ShipHull? hull = ResolvePreferredHeavyHull();
                if (hull == null) return;

                int existingCount = party.Ships?.Count() ?? 0;
                int missingCount = requiredCount - existingCount;
                if (missingCount <= 0) return;

                for (int i = 0; i < missingCount; i++)
                {
                    Ship ship = new Ship(hull);
                    ChangeShipOwnerAction.ApplyByMobilePartyCreation(party.Party, ship);
                }

                party.SetNavalVisualAsDirty();
            }
            catch { }
        }

        /// <summary>
        /// War Sails normally sells AI-clan surplus ships only to a shipyard owned
        /// by that clan's map faction. The landless Grey Wardens can therefore
        /// accumulate captured ships indefinitely. Once per day, each eligible
        /// Warden lord sells at most one tradeable surplus ship to the nearest
        /// non-hostile working shipyard. The native trade action keeps the ship as
        /// a real physical asset at the port and credits the clan leader, whose
        /// wallet is the judicial treasury.
        /// </summary>
        private static void SellSurplusPoliceShips()
        {
            if (!_navalDlcLoaded) return;

            Clan? policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null || policeClan.IsEliminated) return;

            foreach (MobileParty party in PoliceStats.GetAllPoliceParties()
                         .Where(CanSellSurplusShip)
                         .OrderBy(static candidate => candidate.StringId,
                             StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                int requiredCount = GetRequiredShipCount(party);
                int currentCount = party.Ships?.Count() ?? 0;
                if (currentCount <= requiredCount) continue;

                Town? buyer = FindNearestNonHostileShipyard(party, policeClan);
                if (buyer == null) continue;

                Ship? ship = party.Ships
                    .Where(static candidate => candidate != null && candidate.IsTradeable)
                    .Select(candidate => new
                    {
                        Ship = candidate,
                        Value = (int)Campaign.Current.Models.ShipCostModel
                            .GetShipTradeValue(candidate, party.Party,
                                buyer.Settlement.Party)
                    })
                    .Where(static candidate => candidate.Value > 0)
                    .OrderBy(static candidate => candidate.Value)
                    .ThenBy(static candidate =>
                        candidate.Ship.ShipHull?.StringId ?? string.Empty,
                        StringComparer.OrdinalIgnoreCase)
                    .Select(static candidate => candidate.Ship)
                    .FirstOrDefault();
                if (ship == null) continue;

                int saleValue = Math.Max(0, (int)Campaign.Current.Models.ShipCostModel
                    .GetShipTradeValue(ship, party.Party, buyer.Settlement.Party));
                string hullId = ship.ShipHull?.StringId ?? string.Empty;
                int treasuryBefore = GetJudicialTreasuryBalance();

                ChangeShipOwnerAction.ApplyByTrade(buyer.Settlement.Party, ship);

                GwpAiDiagnostics.WriteAction(party, "SURPLUS_SHIP_SOLD",
                    "hull=" + hullId +
                    "; buyer=" + buyer.Settlement.StringId +
                    "; value=" + saleValue +
                    "; shipsBefore=" + currentCount +
                    "; shipsAfter=" + (party.Ships?.Count() ?? 0) +
                    "; required=" + requiredCount +
                    "; treasuryBefore=" + treasuryBefore +
                    "; treasuryAfter=" + GetJudicialTreasuryBalance());
            }
        }

        private static bool CanSellSurplusShip(MobileParty? party)
        {
            return party?.IsActive == true &&
                   party.IsLordParty &&
                   !party.IsDisbanding &&
                   party.LeaderHero?.IsActive == true &&
                   party.MapEvent == null &&
                   party.SiegeEvent == null &&
                   !party.IsCurrentlyAtSea;
        }

        private static Town? FindNearestNonHostileShipyard(
            MobileParty party, Clan policeClan)
        {
            return Town.AllTowns
                .Where(static town => town != null && !town.IsUnderSiege)
                .Where(static town => town.Buildings.Any(building =>
                    building?.BuildingType != null &&
                    building.CurrentLevel > 0 &&
                    string.Equals(building.BuildingType.StringId,
                        ShipyardBuildingTypeId,
                        StringComparison.OrdinalIgnoreCase)))
                .Where(town => town.MapFaction == null ||
                    !FactionManager.IsAtWarAgainstFaction(policeClan,
                        town.MapFaction))
                .OrderBy(town => town.Settlement.GetPosition2D
                    .Distance(party.GetPosition2D))
                .ThenBy(town => town.Settlement.StringId,
                    StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        private static void OnShipOwnerChanged(
            Ship ship,
            PartyBase oldOwner,
            ChangeShipOwnerAction.ShipOwnerChangeDetail detail)
        {
            MobileParty? oldParty = oldOwner?.MobileParty;
            MobileParty? newParty = ship?.Owner?.MobileParty;
            bool oldPolice = IsPoliceLordParty(oldParty);
            bool newPolice = IsPoliceLordParty(newParty);
            if (oldPolice == newPolice) return;

            MobileParty? party = newPolice ? newParty : oldParty;
            if (party == null) return;

            GwpAiDiagnostics.WriteAction(
                party,
                newPolice ? "SHIP_ACQUIRED" : "SHIP_DISPOSED",
                "hull=" + (ship?.ShipHull?.StringId ?? string.Empty) +
                "; detail=" + detail +
                "; oldOwner=" + DescribeShipOwner(oldOwner) +
                "; newOwner=" + DescribeShipOwner(ship?.Owner));
        }

        private static bool IsPoliceLordParty(MobileParty? party) =>
            party?.IsLordParty == true &&
            string.Equals(party.ActualClan?.StringId,
                PoliceStats.PoliceClanId,
                StringComparison.OrdinalIgnoreCase);

        private static string DescribeShipOwner(PartyBase? owner)
        {
            if (owner == null) return "-";
            if (owner.IsSettlement)
                return owner.Settlement?.StringId ?? "settlement";
            return owner.MobileParty?.StringId ?? "party";
        }


        private static int GetRequiredShipCount(MobileParty party)
        {
            int troopCount = Math.Max(1, party?.MemberRoster?.TotalManCount ?? 0);
            return Math.Max(1, (troopCount + TroopsPerShip - 1) / TroopsPerShip);
        }

        private static ShipHull? ResolvePreferredHeavyHull()
        {
            List<ShipHull> hulls = Kingdom.All
                .SelectMany(k => k.Culture.AvailableShipHulls)
                .Where(h => h != null)
                .GroupBy(h => h.StringId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            string[] preferredIds =
            {
                "sturgia_heavy_ship",
                "vlandia_heavy_ship",
                "empire_heavy_ship",
                "aserai_heavy_ship",
                "ship_meditheavy_storyline"
            };

            foreach (string preferredId in preferredIds)
            {
                ShipHull? preferred = hulls.FirstOrDefault(h =>
                    string.Equals(h.StringId, preferredId, StringComparison.OrdinalIgnoreCase));
                if (preferred != null)
                    return preferred;
            }

            ShipHull? fallbackHeavy = hulls.FirstOrDefault(h =>
                string.Equals(h.Type.ToString(), "heavy", StringComparison.OrdinalIgnoreCase));
            if (fallbackHeavy != null)
                return fallbackHeavy;

            return null;
        }

        #endregion

        #region 罚款收取

        public static int CollectFine(int fine)
        {
            if (fine <= 0) return 0;

            int goldTaken = TransferPlayerGoldToJudicialTreasury(fine);

            int remaining = fine - goldTaken;
            int itemsValue = 0;

            if (remaining > 0)
            {
                itemsValue = ConfiscateItems(remaining);
                if (itemsValue > 0)
                {
                    CreditJudicialTreasury(itemsValue);
                    InformationManager.DisplayMessage(new InformationMessage(
                        GwpText.Get("{=gwp_policeresourcemanager_001}Coin is insufficient; goods worth a further {VAR_1} denars have been confiscated.", "VAR_1", itemsValue), Colors.Yellow));
                }
            }

            return goldTaken + itemsValue;
        }

        /// <summary>
        /// 只收金币，不没收背包物品。用于战败押送后的严肃罚款流程。
        /// </summary>
        public static int CollectFineGoldOnly(int fine)
        {
            if (fine <= 0) return 0;
            return TransferPlayerGoldToJudicialTreasury(fine);
        }

        /// <summary>
        /// Player-request payments are all-or-nothing and always enter the clan
        /// leader's wallet, which is the Grey Warden public treasury.
        /// </summary>
        internal static bool CanCollectPlayerRequestPayment(int amount)
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            return amount > 0 && Hero.MainHero != null &&
                   Hero.MainHero.Gold >= amount &&
                   treasurer != null && !treasurer.IsDead &&
                   treasurer != Hero.MainHero;
        }

        internal static bool TryCollectPlayerRequestPayment(int amount)
        {
            if (!CanCollectPlayerRequestPayment(amount)) return false;
            return TransferPlayerGoldToJudicialTreasury(amount) == amount;
        }

        internal static void RefundPlayerRequestPayment(int amount)
        {
            if (amount <= 0 || Hero.MainHero == null) return;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            int treasuryRefund = treasurer?.IsDead == false &&
                                 treasurer != Hero.MainHero
                ? Math.Min(Math.Max(0, treasurer.Gold), amount)
                : 0;
            if (treasuryRefund > 0)
                GiveGoldAction.ApplyBetweenCharacters(treasurer, Hero.MainHero,
                    treasuryRefund, disableNotification: true);
            int remainder = amount - treasuryRefund;
            if (remainder > 0)
                Hero.MainHero.ChangeHeroGold(remainder);
        }

        /// <summary>
        /// 族长的钱包就是原版家族金库，也作为灰袍司法公库。无论罚款由常驻
        /// 领主还是临时纠察队代收，金币都从玩家真实转入族长，不留在经手队伍。
        /// </summary>
        private static int TransferPlayerGoldToJudicialTreasury(int requested)
        {
            if (requested <= 0 || Hero.MainHero == null) return 0;

            int amount = Math.Min(Hero.MainHero.Gold, requested);
            if (amount <= 0) return 0;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer != null && treasurer != Hero.MainHero && !treasurer.IsDead)
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, treasurer, amount,
                    disableNotification: true);
            else
                Hero.MainHero.ChangeHeroGold(-amount);

            return amount;
        }

        /// <summary>
        /// 罚没物离开玩家背包后视为由灰袍统一拍卖，估值直接归司法公库。
        /// </summary>
        internal static void CreditSuccessfulCaseCompletion() =>
            CreditJudicialTreasury(SuccessfulCaseReward);

        /// <summary>
        /// The clan leader's wallet is the judicial treasury. This read-only view is
        /// used by the case ledger and never transfers money between parties.
        /// </summary>
        internal static int GetJudicialTreasuryBalance()
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            return treasurer?.IsDead == false ? Math.Max(0, treasurer.Gold) : 0;
        }

        internal static bool CanFundVillageReconstruction(out int cost, out int reserve)
        {
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            int treasury = GetJudicialTreasuryBalance();
            cost = CalculateVillageReconstructionCost(treasury);
            int dailyWages = PoliceStats.GetAllPoliceParties()
                .Where(party => party?.IsActive == true && party.LeaderHero != null)
                .Sum(party => Math.Max(0, party.TotalWage));
            reserve = Math.Max(
                GwpTuning.Reconstruction.MinimumTreasuryReserve,
                dailyWages * GwpTuning.Reconstruction.WageReserveDays);
            return treasurer != null && !treasurer.IsDead && treasury - cost >= reserve;
        }

        internal static bool TrySpendVillageReconstructionFunds(out int cost, out int reserve)
        {
            if (!CanFundVillageReconstruction(out cost, out reserve))
                return false;

            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer == null || treasurer.IsDead)
                return false;
            treasurer.ChangeHeroGold(-cost);
            return true;
        }

        internal static void RefundJudicialTreasury(int amount) =>
            CreditJudicialTreasury(amount);

        private static int CalculateVillageReconstructionCost(int treasury)
        {
            int proportional = (int)Math.Round(
                treasury * GwpTuning.Reconstruction.TreasuryShare / 100d,
                MidpointRounding.AwayFromZero) * 100;
            return Math.Max(GwpTuning.Reconstruction.MinimumCost,
                Math.Min(GwpTuning.Reconstruction.MaximumCost, proportional));
        }

        private static void CreditJudicialTreasury(int amount)
        {
            if (amount <= 0) return;
            Hero? treasurer = PoliceStats.GetPoliceClan()?.Leader;
            if (treasurer != null && !treasurer.IsDead)
                treasurer.ChangeHeroGold(amount);
        }

        private static int ConfiscateItems(int debt)
        {
            var roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null || roster.Count == 0) return 0;

            var elements = new List<(EquipmentElement eq, int amount, int value)>();
            foreach (ItemRosterElement e in roster)
            {
                var item = e.EquipmentElement.Item;
                if (item == null || e.Amount <= 0 || item.Value <= 0) continue;
                elements.Add((e.EquipmentElement, e.Amount, item.Value));
            }
            elements.Sort((a, b) => b.value.CompareTo(a.value));

            int confiscated = 0;
            foreach (var (eq, amount, value) in elements)
            {
                if (debt <= 0) break;
                int take = Math.Min(amount, (int)Math.Ceiling((double)debt / value));
                roster.AddToCounts(eq, -take);
                int gained = value * take;
                confiscated += gained;
                debt -= gained;
            }
            return confiscated;
        }

        #endregion

        /// <summary>
        /// 旧版兼容入口：不再发出移动命令，只让原版 AI 在下一次拍卖中重算。
        /// </summary>
        public static void ForceImmediateMoveToResupply(MobileParty police)
        {
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(police);
        }

    }
}
