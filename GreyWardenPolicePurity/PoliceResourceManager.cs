using System;
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

        #region 每日发薪 + 建队

        private void OnGameLoaded(CampaignGameStarter starter)
        {
            SpawnIdleHeroes();
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
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
            SpawnIdleHeroes();
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

                GivePoliceShips(newParty);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(newParty);
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
            if (!party.IsActive || !IsPoliceClanHero(party.LeaderHero)) return;
            GreyWardenPartyDesireBehavior.RequestImmediateRethink(party);
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
