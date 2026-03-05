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
        private const string CommanderTemplateCharacterId = "gw_leader_0";

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
        private const string PoliceRecruitId = "gwrecruit";

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

        private void OnGameLoaded(CampaignGameStarter starter) => SpawnIdleHeroes();

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
                if (hero == null || hero.IsDead || !hero.IsActive) continue;
                if (!IsPoliceClanHero(hero)) continue;
                if (hero.IsChild || hero.PartyBelongedTo != null || hero.IsPrisoner) continue;
                if (!hero.CanLeadParty()) continue;

                try
                {
                    ApplyCommanderLoadout(hero);
                    Settlement spawn = hero.CurrentSettlement
                        ?? FindNearestTown(policeClan.Leader?.PartyBelongedTo?.GetPosition2D ?? Vec2.Zero);
                    MobileParty newParty = MobilePartyHelper.SpawnLordParty(hero, spawn);
                    if (newParty != null)
                    {
                        ReplenishTroops(newParty);
                        ReplenishFood(newParty);
                        GivePoliceShips(newParty);   // 按人数比例配船
                    }
                }
                catch (Exception ex)
                {
                    // 内部组队失败（开发错误日志，正式版静默忽略）
                    _ = ex;
                }
            }
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

            CharacterObject template = CharacterObject.Find(CommanderTemplateCharacterId)
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
                else
                {
                    DoResupply(party);
                    toFinish.Add(partyId);
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
                ReleasePrisoners(police);
                ReplenishTroops(police);
                ReplenishFood(police);
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
            var recruit = CharacterObject.Find(PoliceRecruitId);
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
            if (character == null) return false;
            string id = character.StringId;
            return id != "gwrecruit" && id != "gwheavyinfantry" && id != "gwarcher" && id != "gwknight";
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

            ItemObject foodItem = MBObjectManager.Instance.GetObject<ItemObject>("grain")
                ?? MBObjectManager.Instance.GetObject<ItemObject>(o => o is ItemObject item && item.IsFood) as ItemObject;
            if (foodItem == null) return;

            police.ItemRoster.AddToCounts(foodItem, needed);
        }

        /// <summary>
        /// 给警察部队按人数比例配发初始船只（每100人1艘，最少1艘；无 NavalDLC 则静默跳过）
        /// </summary>
        internal static void GivePoliceShips(MobileParty party)
        {
            if (!_navalDlcLoaded) return;
            try
            {
                // 按部队规模计算船数：每50人配1艘，最少1艘
                int count = Math.Max(1, party.MemberRoster.TotalManCount / 50);

                // 优先 dromon（中型战船），其次 liburna（轻型战舰），最后随机
                ShipHull? hull = Kingdom.All
                    .SelectMany(k => k.Culture.AvailableShipHulls)
                    .FirstOrDefault(h => h.StringId == "dromon")
                    ?? Kingdom.All.SelectMany(k => k.Culture.AvailableShipHulls)
                                  .FirstOrDefault(h => h.StringId == "liburna")
                    ?? Kingdom.All.SelectMany(k => k.Culture.AvailableShipHulls)
                                  .FirstOrDefault();
                if (hull == null) return;
                for (int i = 0; i < count; i++)
                {
                    Ship ship = new Ship(hull);
                    ChangeShipOwnerAction.ApplyByMobilePartyCreation(party.Party, ship);
                }
                party.SetNavalVisualAsDirty();
            }
            catch { }
        }

        private static void ReleasePrisoners(MobileParty police)
        {
            if (police.PrisonRoster.TotalManCount > 0)
                police.PrisonRoster.Clear();
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
