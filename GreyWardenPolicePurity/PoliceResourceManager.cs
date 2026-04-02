﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Helpers;
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
    /// 1. 每日发薪：每人每天1000金
    /// 2. 任务结束后：前往最近城镇补给（补兵+释放俘虏+补粮）
    /// 3. 每6小时：净化各警察部队兵种（替换非法兵为新兵）
    /// </summary>
    public class PoliceResourceManager : CampaignBehaviorBase
    {
        private const int DailyGoldPerMember = 1000;
        private const int FoodDaysTarget = 15;
        private const int EquipmentSlotCount = 12;
        private const int TroopsPerShip = 50;
        // 正在补给中的警察部队ID
        private static readonly HashSet<string> _resupplying = new HashSet<string>();

        // NavalDLC 可选依赖：运行时一次性检测（所有模块 DLL 加载后）
        // 若 NavalDLC 未安装，GivePoliceShips 直接 return，不影响游玩
        private static readonly bool _navalDlcLoaded =
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "NavalDLC");

        // 每个部队的上次兵员净化时间（小时）
        private Dictionary<string, double> _lastPurifyTime =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private const double PurifyIntervalHours = 6.0;
        public static bool IsResupplying(MobileParty party) =>
            party != null && _resupplying.Contains(party.StringId);

        public static bool IsReady(MobileParty party)
        {
            if (party == null || !party.IsActive) return false;
            return !_resupplying.Contains(party.StringId);
        }

        public static void CancelResupply(MobileParty police)
        {
            if (police == null) return;
            _resupplying.Remove(police.StringId);
        }

        public static void StartResupply(MobileParty police)
        {
            if (police == null || !police.IsActive) return;
            if (_resupplying.Contains(police.StringId)) return;
            // 只标记，实际移动由每小时Tick处理（战斗刚结束时移动命令会被引擎覆盖）
            _resupplying.Add(police.StringId);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, OnGameLoaded);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.HourlyTickPartyEvent.AddNonSerializedListener(this, OnHourlyTickParty);
            CampaignEvents.HeroComesOfAgeEvent.AddNonSerializedListener(this, OnHeroComesOfAge);
        }

        public override void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsLoading)
            {
                _resupplying.Clear();
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

        #region 每日发薪 + 建队

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            SpawnIdleHeroes();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
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

            PaySalaries();
            SpawnIdleHeroes();
        }

        private void SpawnIdleHeroes()
        {
            Clan policeClan = PoliceStats.GetPoliceClan();
            if (policeClan == null) return;

            foreach (Hero hero in policeClan.Heroes.ToList())
            {
                RecoverPoliceCommanderParty(hero, policeClan);
            }
        }

        private void RecoverPoliceCommanderParty(Hero? hero, Clan policeClan)
        {
            if (!IsEligiblePoliceCommander(hero))
                return;

            if (hero == null)
                return;

            try
            {
                EnsurePoliceCommanderIsActive(hero);
                ApplyCommanderLoadout(hero);

                MobileParty? existingParty = hero.PartyBelongedTo;
                if (existingParty?.IsActive == true)
                {
                    RecoverPoliceShellPartyIfNeeded(existingParty);
                    return;
                }

                if (hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null)
                    return;

                if (existingParty != null && !TryClearBrokenPartyReference(hero))
                    return;

                Settlement? spawn = ResolvePoliceSpawnSettlement(hero, policeClan);
                if (spawn == null)
                    return;

                PreparePoliceCommanderForSpawn(hero, spawn);

                MobileParty? newParty = MobilePartyHelper.SpawnLordParty(hero, spawn);
                if (newParty == null)
                    return;

                RecoverPolicePartySupplies(newParty);
            }
            catch (Exception ex)
            {
                // 内部组队失败（开发错误日志，正式版静默忽略）
                _ = ex;
            }
        }

        private static bool IsEligiblePoliceCommander(Hero? hero)
        {
            if (!GwpCommon.IsGreyWardenLord(hero))
                return false;

            if (hero == null || hero.IsDead || hero.IsDisabled || hero.IsChild)
                return false;

            return hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
        }

        private static void EnsurePoliceCommanderIsActive(Hero hero)
        {
            if (hero.IsActive || hero.IsPrisoner || hero.IsDisabled || hero.IsDead)
                return;

            try { hero.ChangeState(Hero.CharacterStates.Active); } catch { }
        }

        private static Settlement? ResolvePoliceSpawnSettlement(Hero hero, Clan policeClan)
        {
            if (hero.CurrentSettlement?.IsTown == true && hero.CurrentSettlement.SiegeEvent == null)
                return hero.CurrentSettlement;

            if (hero.HomeSettlement?.IsTown == true && hero.HomeSettlement.SiegeEvent == null)
                return hero.HomeSettlement;

            Settlement? bestSettlement = SettlementHelper.GetBestSettlementToSpawnAround(hero);
            if (bestSettlement?.IsTown == true && bestSettlement.SiegeEvent == null)
                return bestSettlement;

            if (policeClan.InitialHomeSettlement?.IsTown == true && policeClan.InitialHomeSettlement.SiegeEvent == null)
                return policeClan.InitialHomeSettlement;

            Vec2 fallbackPosition = hero.CurrentSettlement?.GetPosition2D
                ?? hero.HomeSettlement?.GetPosition2D
                ?? policeClan.Leader?.CurrentSettlement?.GetPosition2D
                ?? policeClan.Leader?.PartyBelongedTo?.GetPosition2D
                ?? Vec2.Zero;

            return FindNearestTown(fallbackPosition);
        }

        private static void PreparePoliceCommanderForSpawn(Hero hero, Settlement spawn)
        {
            if (hero.GovernorOf != null)
            {
                try { ChangeGovernorAction.RemoveGovernorOf(hero); } catch { }
            }

            try { hero.StayingInSettlement = null; } catch { }

            if (hero.CurrentSettlement != spawn)
            {
                try { TeleportHeroAction.ApplyImmediateTeleportToSettlement(hero, spawn); } catch { }
            }
        }

        private static bool TryClearBrokenPartyReference(Hero hero)
        {
            if (hero.PartyBelongedTo == null)
                return true;

            if (hero.PartyBelongedTo.IsActive)
                return false;

            try
            {
                typeof(Hero)
                    .GetMethod("SetPartyBelongedTo", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(hero, new object?[] { null });
            }
            catch { }

            return hero.PartyBelongedTo == null;
        }

        private static void RecoverPoliceShellPartyIfNeeded(MobileParty party)
        {
            if (!party.IsActive || !IsPoliceClanHero(party.LeaderHero))
                return;

            if (party.MemberRoster.TotalRegulars > 0)
                return;

            if (party.CurrentSettlement == null || !party.CurrentSettlement.IsTown)
            {
                StartResupply(party);
                return;
            }

            RecoverPolicePartySupplies(party);
        }

        private static void RecoverPolicePartySupplies(MobileParty party)
        {
            ReplenishTroops(party);
            ReplenishFood(party);
            GivePoliceShips(party);
            CancelResupply(party);
            GwpCommon.TryResetAi(party);
        }

        private void OnHeroComesOfAge(Hero hero)
        {
            if (hero == null || !IsPoliceClanHero(hero)) return;
            ApplyCommanderLoadout(hero);
        }

        private static bool IsPoliceClanHero(Hero hero)
        {
            return hero?.Clan != null &&
                   string.Equals(hero.Clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyCommanderLoadout(Hero hero)
        {
            if (hero == null) return;

            CharacterObject? template = CharacterObject.Find(GwpIds.CommanderTemplateCharacterId)
                ?? hero.Clan?.Leader?.CharacterObject;
            if (template == null) return;

            Equipment battleTemplate = template.FirstBattleEquipment;
            Equipment civilianTemplate = template.FirstCivilianEquipment;
            if (battleTemplate == null || battleTemplate.IsEmpty()) return;
            if (civilianTemplate == null || civilianTemplate.IsEmpty()) return;

            // 先确保英雄拥有独立装备实例，避免写入共享的默认死者装备。
            hero.ResetEquipments();
            CopyEquipment(battleTemplate, hero.BattleEquipment);
            CopyEquipment(civilianTemplate, hero.CivilianEquipment);
            hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
        }

        private static void CopyEquipment(Equipment source, Equipment destination)
        {
            if (source == null || destination == null) return;
            for (int i = 0; i < EquipmentSlotCount; i++)
                destination[i] = source[i];
        }

        private void PaySalaries()
        {
            foreach (var party in PoliceStats.GetAllPoliceParties())
            {
                Hero leader = party.LeaderHero;
                if (leader == null) continue;
                int salary = party.Party.NumberOfAllMembers * DailyGoldPerMember;
                leader.ChangeHeroGold(salary);
            }
        }

        #endregion

        #region 补给流程

        private void OnHourlyTick() => CheckResupplyingParties();

        private void CheckResupplyingParties()
        {
            var toFinish = new List<string>();

            foreach (string partyId in _resupplying)
            {
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == partyId);

                if (party == null || !party.IsActive)
                {
                    toFinish.Add(partyId);
                    continue;
                }

                // 押送玩家中，跳过补给
                if (CrimePool.ActiveTasks.Values.Any(t => t.PolicePartyId == partyId && t.IsEscortingPlayer))
                    continue;

                if (party.CurrentSettlement != null && party.CurrentSettlement.IsTown)
                {
                    DoResupply(party);
                    toFinish.Add(partyId);
                    continue;
                }

                Settlement nearestTown = FindNearestTown(party.GetPosition2D);
                if (nearestTown != null)
                {
                    party.Ai.SetDoNotMakeNewDecisions(true);
                    party.SetMoveGoToSettlement(nearestTown, NavigationType.Default, false);
                }
            }

            foreach (string id in toFinish)
            {
                var party = MobileParty.All.FirstOrDefault(p => p.StringId == id);
                if (party != null && party.IsActive)
                    FinishResupply(party);
                _resupplying.Remove(id);
            }
        }

        private static void DoResupply(MobileParty police)
        {
            try
            {
                ReleasePrisoners(police, police.CurrentSettlement);
                ReplenishTroops(police);
                ReplenishFood(police);
                GivePoliceShips(police);
            }
            catch (Exception ex)
            {
                // 内部补给失败（开发错误日志，正式版静默忽略）
                _ = ex;
            }
        }

        private static void FinishResupply(MobileParty police)
        {
            police.Ai.SetDoNotMakeNewDecisions(false);
            police.Ai.SetInitiative(0f, 0f, 0f);
            // 内部补给完成日志（开发调试，正式版不显示）
            // InformationManager.DisplayMessage(new InformationMessage(
            //     $"[GWP 补给] {police.Name} 补给完成（兵员=...）",
            //     Colors.Green));
        }

        #endregion

        #region 兵员纯化（每6小时）

        private void OnHourlyTickParty(MobileParty party)
        {
            if (party == null || party.IsCaravan || party.IsMilitia || party.IsVillager) return;

            var clan = party.LeaderHero?.Clan;
            if (clan == null) return;
            if (!string.Equals(clan.StringId, PoliceStats.PoliceClanId, StringComparison.OrdinalIgnoreCase)) return;

            double now = CampaignTime.Now.ToHours;
            if (_lastPurifyTime.TryGetValue(party.StringId, out double lastCheck) &&
                now - lastCheck < PurifyIntervalHours) return;

            _lastPurifyTime[party.StringId] = now;
            PurifyParty(party);
        }

        private void PurifyParty(MobileParty party)
        {
            var recruit = CharacterObject.Find(GwpIds.PoliceRecruitId);
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

        private static void ReplenishTroops(MobileParty police)
        {
            int needed = police.Party.PartySizeLimit - police.Party.NumberOfAllMembers;
            if (needed <= 0) return;

            CharacterObject infantry = MBObjectManager.Instance.GetObject<CharacterObject>("gwheavyinfantry");
            CharacterObject ranged = MBObjectManager.Instance.GetObject<CharacterObject>("gwarcher");
            if (infantry == null && ranged == null) return;

            int infantryCount = needed / 2;
            int rangedCount = needed - infantryCount;

            if (infantry != null && infantryCount > 0)
                police.MemberRoster.AddToCounts(infantry, infantryCount);
            if (ranged != null && rangedCount > 0)
                police.MemberRoster.AddToCounts(ranged, rangedCount);
        }

        public static void ReplenishFood(MobileParty police, int foodDays = FoodDaysTarget)
        {
            int needed = police.Party.NumberOfAllMembers * foodDays - police.ItemRoster.TotalFood;
            if (needed <= 0) return;

            ItemObject foodItem = MBObjectManager.Instance.GetObject<ItemObject>(GwpIds.GrainItemId)
                ?? MBObjectManager.Instance.GetObject<ItemObject>(o => o is ItemObject item && item.IsFood) as ItemObject;
            if (foodItem == null) return;

            police.ItemRoster.AddToCounts(foodItem, needed);
        }

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

        private static void ReleasePrisoners(MobileParty police, Settlement? settlement)
        {
            if (police == null || police.PrisonRoster.TotalManCount <= 0)
                return;

            if (settlement?.Party == null)
                return;

            foreach (TroopRosterElement prisoner in police.PrisonRoster.GetTroopRoster().ToList())
            {
                CharacterObject? character = prisoner.Character;
                if (character?.HeroObject == null) continue;

                TransferPrisonerAction.Apply(character, police.Party, settlement.Party);
            }

            if (police.PrisonRoster.TotalManCount > 0)
                SellPrisonersAction.ApplyForAllPrisoners(police.Party, settlement.Party);
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

            int gold = Hero.MainHero.Gold;
            int goldTaken = Math.Min(gold, fine);
            if (goldTaken > 0)
                Hero.MainHero.ChangeHeroGold(-goldTaken);

            int remaining = fine - goldTaken;
            int itemsValue = 0;

            if (remaining > 0)
            {
                itemsValue = ConfiscateItems(remaining);
                if (itemsValue > 0)
                    InformationManager.DisplayMessage(new InformationMessage(
                        $"金币不足，额外没收物品价值 {itemsValue} 金", Colors.Yellow));
            }

            return goldTaken + itemsValue;
        }

        /// <summary>
        /// 只收金币，不没收背包物品。用于战败押送后的严肃罚款流程。
        /// </summary>
        public static int CollectFineGoldOnly(int fine)
        {
            if (fine <= 0) return 0;

            int goldTaken = Math.Min(Hero.MainHero.Gold, fine);
            if (goldTaken > 0)
                Hero.MainHero.ChangeHeroGold(-goldTaken);

            return goldTaken;
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
        /// 立即给警察部队发往最近城镇的移动命令（不等每小时 tick）。
        /// 用于对话缴罚款后立刻将部队重定向，防止其停在玩家接触范围内再次触发对话循环。
        /// 模式与 PolicePatrolBehavior.ReturnAllPatrols() 一致：DoNotMakeNewDecisions + SetMoveGoToSettlement。
        /// </summary>
        public static void ForceImmediateMoveToResupply(MobileParty police)
        {
            if (police == null || !police.IsActive) return;
            Settlement nearestTown = FindNearestTown(police.GetPosition2D);
            if (nearestTown != null)
            {
                police.Ai.SetDoNotMakeNewDecisions(true);
                police.SetMoveGoToSettlement(nearestTown, NavigationType.Default, false);
            }
        }

        private static Settlement FindNearestTown(Vec2 position)
        {
            Settlement nearest = null!;
            float minDist = float.MaxValue;
            foreach (Settlement s in Settlement.All)
            {
                if (!s.IsTown) continue;
                float dist = position.Distance(s.GetPosition2D);
                if (dist < minDist) { minDist = dist; nearest = s; }
            }
            return nearest;
        }
    }
}
