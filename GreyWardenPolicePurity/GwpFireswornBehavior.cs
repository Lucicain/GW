using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者：信众村、祭司、守村队、地牢与烧人、传教、信众队、军械库
    /// （docs/state/firesworn.md 为入口，各分系统独立维护）。
    ///
    /// 有火誓者的村子在 <see cref="_villages"/> 里有一条记录；有了祭司才算信众村（用户裁定 2026-10-07）。
    /// 两本账（用户裁定 2026-10-07：兵走了家还在；2026-10-08：两户出一个兵）：
    /// <list type="bullet">
    /// <item>信众户（<see cref="FaithVillage.Households"/>）：信教的人家，不超过户数。火誓者比例 = 信众户 ÷ 户数。</item>
    /// <item>守村队（<see cref="_guards"/>）：信众户出的兵，自己补兵不超过 <see cref="GuardCap"/>，外来并入和烧人产兵可超过。和原版民兵站在同一支民兵队里，
    /// 原版民兵照原版增长、参战，是烧人的原料。兵被招走、战死、出门，信众户和户数都不变，守村队慢慢补回。</item>
    /// </list>
    ///
    /// 分文件：本文件是数据、存档、查询、事件分派、祭司、守村队；仪式见 GwpFireswornRituals.cs，
    /// 传教队、卖货队、运械队见 GwpFireswornParties.cs，地牢与领主进村交接见 GwpFireswornDungeon.cs，军械库见 GwpFireswornArmory.cs。
    /// </summary>
    public sealed partial class GwpFireswornBehavior : CampaignBehaviorBase
    {
        private const double Never = -1000000d;

        private static GwpFireswornBehavior? _instance;

        private readonly Dictionary<string, FaithVillage> _villages =
            new Dictionary<string, FaithVillage>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Ritual> _rituals = new List<Ritual>();
        private readonly Dictionary<string, FaithParty> _faithParties =
            new Dictionary<string, FaithParty>(StringComparer.OrdinalIgnoreCase);
        // 守村队：每座有火誓者的村子里的灰袍兵，各等级都有。人数就是这座村的火誓者人数。
        private readonly Dictionary<string, TroopRoster> _guards =
            new Dictionary<string, TroopRoster>(StringComparer.OrdinalIgnoreCase);
        // 信众村地牢：灰袍领主交来的俘虏、守村俘获的敌兵，等着烧。
        private readonly Dictionary<string, TroopRoster> _dungeons =
            new Dictionary<string, TroopRoster>(StringComparer.OrdinalIgnoreCase);
        // 信众村的货仓：领主捐的、军械库用不完的东西，由信众队运进城卖。
        private readonly Dictionary<string, ItemRoster> _goods =
            new Dictionary<string, ItemRoster>(StringComparer.OrdinalIgnoreCase);
        // 军械库账本：键是有火誓者的村子或灰袍领主部队的 StringId。兵身上穿的也算在里面。
        private readonly Dictionary<string, ItemRoster> _armories =
            new Dictionary<string, ItemRoster>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _priestIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal enum RitualKind
        {
            Congregation = 0,
            Damnation = 1
        }

        /// <summary>
        /// 三种无领主队（用户裁定 2026-10-07）：传教队把人送过去，到了就并入当地、队伍没了；
        /// 卖货队照原版村民队常驻，卖完回村；运械队把兵器运到缺的村子，到了也并入当地、队伍没了。
        /// </summary>
        internal enum FaithPartyKind
        {
            Mission = 0,
            Market = 1,
            Balance = 2,
            Training = 3
        }

        private enum FaithPartyPhase
        {
            Home = 0,
            Outbound = 1,
            Returning = 2
        }

        private sealed class FaithVillage
        {
            public string VillageId = string.Empty;
            public double LastCongregation = Never;
            public double LastDamnation = Never;
            public double LastMission = Never;
            public double LastBalance = Never;
            public string PriestId = string.Empty;
            // 信众户：信教的人家，不超过户数。
            public int Households;
            // 派过几次传教队：祭司最强的信众村之间拉平（用户裁定 2026-10-07）。
            public int MissionsSent;
        }

        /// <summary>集会传教由祭司在本村主持，烈火责罚由村子自己办；都锁村，玩家进村只看到仪式画面。</summary>
        private sealed class Ritual
        {
            public string VillageId = string.Empty;
            public RitualKind Kind;
            public double EndHour;
        }

        /// <summary>一支无领主队：哪一种、从哪来、在哪个阶段、去哪儿。传教队、运械队只有出发这一段。</summary>
        private sealed class FaithParty
        {
            public string PartyId = string.Empty;
            public string HomeId = string.Empty;
            public string TrainerId = string.Empty;
            public FaithPartyKind Kind;
            public FaithPartyPhase Phase;
            public string TargetId = string.Empty;
            public double PhaseHour;
            // 在途灰袍货款，实际金币仍在原版交易账户；同版存读不能丢失。
            public int MarketGold;
        }

        public GwpFireswornBehavior()
        {
            _instance = this;
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
            CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, OnDailyTickSettlement);
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnSettlementLeftEvent.AddNonSerializedListener(this, OnSettlementLeft);
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        // ---------------------------------------------------------------- 存档

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? villageIds = null, priests = null;
            List<int>? households = null;
            List<int>? missionsSent = null;
            List<double>? congregations = null, damnations = null, missions = null, balances = null;
            List<string>? ritualVillages = null;
            List<int>? ritualKinds = null;
            List<double>? ritualEnds = null;
            List<string>? partyIds = null, partyHomes = null, partyTargets = null, partyTrainers = null;
            List<string>? trainingHeroes = null;
            List<double>? trainingDepartures = null;
            List<int>? partyKinds = null, partyPhases = null, partyMarketGold = null;
            List<double>? partyHours = null;

            if (dataStore.IsSaving)
            {
                List<FaithVillage> rows = _villages.Values.ToList();
                villageIds = rows.Select(row => row.VillageId).ToList();
                congregations = rows.Select(row => row.LastCongregation).ToList();
                damnations = rows.Select(row => row.LastDamnation).ToList();
                missions = rows.Select(row => row.LastMission).ToList();
                balances = rows.Select(row => row.LastBalance).ToList();
                priests = rows.Select(row => row.PriestId).ToList();
                households = rows.Select(row => row.Households).ToList();
                missionsSent = rows.Select(row => row.MissionsSent).ToList();
                ritualVillages = _rituals.Select(ritual => ritual.VillageId).ToList();
                ritualKinds = _rituals.Select(ritual => (int)ritual.Kind).ToList();
                ritualEnds = _rituals.Select(ritual => ritual.EndHour).ToList();
                List<FaithParty> parties = _faithParties.Values.ToList();
                partyIds = parties.Select(party => party.PartyId).ToList();
                partyTrainers = parties.Select(party => party.TrainerId).ToList();
                trainingHeroes = _trainingDepartures.Keys.ToList();
                trainingDepartures = trainingHeroes.Select(id => _trainingDepartures[id]).ToList();
                partyHomes = parties.Select(party => party.HomeId).ToList();
                partyKinds = parties.Select(party => (int)party.Kind).ToList();
                partyPhases = parties.Select(party => (int)party.Phase).ToList();
                partyTargets = parties.Select(party => party.TargetId).ToList();
                partyHours = parties.Select(party => party.PhaseHour).ToList();
                partyMarketGold = parties.Select(party => party.MarketGold).ToList();
            }

            dataStore.SyncData("GWPP_FsPartyTrainers", ref partyTrainers);
            dataStore.SyncData("GWPP_FsTrainingHeroes", ref trainingHeroes);
            dataStore.SyncData("GWPP_FsTrainingDepartures", ref trainingDepartures);
            dataStore.SyncData("GWPP_FsVillageIds", ref villageIds);
            dataStore.SyncData("GWPP_FsVillageLastCongregation", ref congregations);
            dataStore.SyncData("GWPP_FsVillageLastDamnation", ref damnations);
            dataStore.SyncData("GWPP_FsVillageLastMission", ref missions);
            dataStore.SyncData("GWPP_FsVillageLastBalance", ref balances);
            dataStore.SyncData("GWPP_FsVillagePriest", ref priests);
            dataStore.SyncData("GWPP_FsVillageHouseholds", ref households);
            dataStore.SyncData("GWPP_FsVillageMissionsSent", ref missionsSent);
            dataStore.SyncData("GWPP_FsRitualVillage", ref ritualVillages);
            dataStore.SyncData("GWPP_FsRitualKind", ref ritualKinds);
            dataStore.SyncData("GWPP_FsRitualEnd", ref ritualEnds);
            dataStore.SyncData("GWPP_FsPartyIds", ref partyIds);
            dataStore.SyncData("GWPP_FsPartyHomes", ref partyHomes);
            dataStore.SyncData("GWPP_FsPartyKinds", ref partyKinds);
            dataStore.SyncData("GWPP_FsPartyPhases", ref partyPhases);
            dataStore.SyncData("GWPP_FsPartyTargets", ref partyTargets);
            dataStore.SyncData("GWPP_FsPartyHours", ref partyHours);
            dataStore.SyncData("GWPP_FsPartyMarketGold", ref partyMarketGold);
            SyncTroopRosters(dataStore, "GWPP_FsGuard", _guards);
            SyncTroopRosters(dataStore, "GWPP_FsDungeon", _dungeons);
            SyncItemRosters(dataStore, "GWPP_FsGoods", _goods);
            SyncItemRosters(dataStore, "GWPP_FsArmory", _armories);
            SyncItemRosters(dataStore, "GWPP_FsMarketCargo", _marketCargo);

            if (!dataStore.IsLoading)
                return;

            _villages.Clear();
            for (int i = 0; i < villageIds!.Count; i++)
                _villages[villageIds[i]] = new FaithVillage
                {
                    VillageId = villageIds[i],
                    LastCongregation = congregations![i],
                    LastDamnation = damnations![i],
                    LastMission = missions![i],
                    LastBalance = balances![i],
                    PriestId = priests![i],
                    Households = households![i],
                    MissionsSent = missionsSent![i]
                };

            _rituals.Clear();
            for (int i = 0; i < ritualVillages!.Count; i++)
                _rituals.Add(new Ritual
                {
                    VillageId = ritualVillages[i],
                    Kind = (RitualKind)ritualKinds![i],
                    EndHour = ritualEnds![i]
                });

            _faithParties.Clear();
            for (int i = 0; i < partyIds!.Count; i++)
                _faithParties[partyIds[i]] = new FaithParty
                {
                    PartyId = partyIds[i],
                    TrainerId = partyTrainers![i],
                    HomeId = partyHomes![i],
                    Kind = (FaithPartyKind)partyKinds![i],
                    Phase = (FaithPartyPhase)partyPhases![i],
                    TargetId = partyTargets![i],
                    PhaseHour = partyHours![i],
                    MarketGold = partyMarketGold![i]
                };

            _trainingDepartures.Clear();
            for (int i = 0; i < trainingHeroes!.Count; i++)
                _trainingDepartures[trainingHeroes[i]] = trainingDepartures![i];
            _plans.Clear();
            RebuildPriestIndex();
        }

        private static void SyncTroopRosters(IDataStore dataStore, string key,
            Dictionary<string, TroopRoster> rosters)
        {
            List<string>? holders = null, characters = null;
            List<int>? counts = null, wounded = null, experience = null;
            if (dataStore.IsSaving)
            {
                holders = new List<string>();
                characters = new List<string>();
                counts = new List<int>();
                wounded = new List<int>();
                experience = new List<int>();
                foreach (KeyValuePair<string, TroopRoster> pair in rosters)
                    foreach (TroopRosterElement element in pair.Value.GetTroopRoster())
                    {
                        holders.Add(pair.Key);
                        characters.Add(element.Character.StringId);
                        counts.Add(element.Number);
                        wounded.Add(element.WoundedNumber);
                        experience.Add(element.Xp);
                    }
            }
            dataStore.SyncData(key + "Holders", ref holders);
            dataStore.SyncData(key + "Characters", ref characters);
            dataStore.SyncData(key + "Counts", ref counts);
            dataStore.SyncData(key + "Wounded", ref wounded);
            dataStore.SyncData(key + "Experience", ref experience);
            if (!dataStore.IsLoading)
                return;
            rosters.Clear();
            for (int i = 0; i < holders!.Count; i++)
                RosterOf(rosters, holders[i]).AddToCounts(CharacterObject.Find(characters![i]), counts![i],
                    false, wounded![i], experience![i]);
        }

        private static void SyncItemRosters(IDataStore dataStore, string key,
            Dictionary<string, ItemRoster> rosters)
        {
            List<string>? holders = null, items = null, modifiers = null;
            List<int>? counts = null;
            if (dataStore.IsSaving)
            {
                holders = new List<string>();
                items = new List<string>();
                modifiers = new List<string>();
                counts = new List<int>();
                foreach (KeyValuePair<string, ItemRoster> pair in rosters)
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        ItemRosterElement element = pair.Value.GetElementCopyAtIndex(i);
                        holders.Add(pair.Key);
                        items.Add(element.EquipmentElement.Item.StringId);
                        modifiers.Add(element.EquipmentElement.ItemModifier?.StringId ?? string.Empty);
                        counts.Add(element.Amount);
                    }
            }
            dataStore.SyncData(key + "Holders", ref holders);
            dataStore.SyncData(key + "Items", ref items);
            dataStore.SyncData(key + "Modifiers", ref modifiers);
            dataStore.SyncData(key + "Counts", ref counts);
            if (!dataStore.IsLoading)
                return;
            rosters.Clear();
            for (int i = 0; i < holders!.Count; i++)
            {
                ItemObject item = MBObjectManager.Instance.GetObject<ItemObject>(items![i]);
                ItemModifier? modifier = modifiers![i].Length == 0
                    ? null
                    : MBObjectManager.Instance.GetObject<ItemModifier>(modifiers[i]);
                ItemsOf(rosters, holders[i]).AddToCounts(new EquipmentElement(item, modifier), counts![i]);
            }
        }

        private static TroopRoster RosterOf(Dictionary<string, TroopRoster> rosters, string holder)
        {
            if (!rosters.TryGetValue(holder, out TroopRoster? roster))
            {
                roster = TroopRoster.CreateDummyTroopRoster();
                rosters[holder] = roster;
            }
            return roster;
        }

        private static ItemRoster ItemsOf(Dictionary<string, ItemRoster> rosters, string holder)
        {
            if (!rosters.TryGetValue(holder, out ItemRoster? roster))
            {
                roster = new ItemRoster();
                rosters[holder] = roster;
            }
            return roster;
        }

        // ---------------------------------------------------------------- 查询

        /// <summary>这是不是一支火誓者的无领主队；是的话，哪一种、是不是在去的路上、家在哪、要去哪。</summary>
        internal static bool TryDescribeFaithParty(MobileParty? party, out FaithPartyKind kind, out bool outbound,
            out Settlement? home, out Settlement? target)
        {
            kind = FaithPartyKind.Market;
            outbound = false;
            home = target = null;
            if (party == null || _instance == null || !_instance._faithParties.TryGetValue(party.StringId, out FaithParty? record))
                return false;
            kind = record.Kind;
            home = Settlement.Find(record.HomeId);
            target = Settlement.Find(record.TargetId);
            outbound = record.Phase == FaithPartyPhase.Outbound;
            return true;
        }

        /// <summary>信众村：有祭司的村子（用户裁定 2026-10-07：生成要人以后才算信众）。</summary>
        internal static IEnumerable<Settlement> FaithVillages() =>
            _instance == null
                ? Enumerable.Empty<Settlement>()
                : _instance._villages.Values.Where(row => FindLivingPriest(row) != null)
                    .Select(row => Settlement.Find(row.VillageId)).ToList();

        internal static bool IsPreachedVillage(Settlement? settlement) =>
            settlement?.IsVillage == true && _instance != null &&
            _instance._villages.TryGetValue(settlement.StringId, out FaithVillage? row) && FindLivingPriest(row) != null;

        /// <summary>村里有没有火誓者（有守村队、有军械库），不管有没有祭司。</summary>
        internal static bool HasFireswornGuard(Settlement? settlement) =>
            settlement?.IsVillage == true && _instance != null && _instance._villages.ContainsKey(settlement.StringId);

        internal static bool IsFireswornPriest(Hero? hero) =>
            hero != null && _instance != null && _instance._priestIds.Contains(hero.StringId);

        /// <summary>案子发生在某座信众村跟前：灰袍优先接这种案子（用户裁定）。</summary>
        internal static bool IsNearFaithVillage(Vec2 position) =>
            FaithVillages().Any(village => village.GetPosition2D.Distance(position) <= GwpTuning.Firesworn.FaithCaseRadius);

        /// <summary>村庄菜单要显示的：信众户、守村队人数、祭司。</summary>
        internal static bool TryGetVillageFaith(Settlement? settlement, out int households,
            out int guard, out Hero? priest)
        {
            households = guard = 0;
            priest = null;
            if (settlement?.IsVillage != true || _instance == null ||
                !_instance._villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                return false;
            households = row.Households;
            guard = _instance.Sworn(row);
            priest = FindLivingPriest(row);
            return true;
        }

        /// <summary>村庄菜单的「永恒之火」说明里显示的地牢、军械库、货仓。</summary>
        internal static void GetVillageStores(Settlement settlement, out int prisoners,
            out int armoryItems, out int goodsValue)
        {
            prisoners = armoryItems = goodsValue = 0;
            if (_instance == null)
                return;
            if (_instance._dungeons.TryGetValue(settlement.StringId, out TroopRoster? dungeon))
                prisoners = dungeon.TotalManCount;
            if (_instance._armories.TryGetValue(settlement.StringId, out ItemRoster? armory))
                armoryItems = CountItems(armory);
            if (_instance._goods.TryGetValue(settlement.StringId, out ItemRoster? goods))
                goodsValue = goods.TotalValue;
        }

        /// <summary>村子正在办的仪式。期间玩家进村只看到仪式画面。</summary>
        internal static bool TryGetActiveRitual(Settlement? settlement, out RitualKind kind,
            out double remainingHours, out string hostName)
        {
            kind = RitualKind.Congregation;
            remainingHours = 0d;
            hostName = string.Empty;
            if (settlement?.IsVillage != true || _instance == null)
                return false;
            Ritual? ritual = _instance._rituals.FirstOrDefault(entry =>
                string.Equals(entry.VillageId, settlement.StringId, StringComparison.OrdinalIgnoreCase));
            if (ritual == null)
                return false;
            kind = ritual.Kind;
            remainingHours = Math.Max(0d, ritual.EndHour - CampaignTime.Now.ToHours);
            hostName = _instance.RitualHostName(settlement);
            return true;
        }

        /// <summary>信众户上限就是户数，跟着户数变（用户裁定 2026-10-05）。</summary>
        internal static int HouseholdCap(Settlement settlement) => (int)settlement.Village.Hearth;

        /// <summary>
        /// 灰袍领主在这座信众村能直接招几个守村兵，实际取人按低阶优先（用户裁定 2026-10-15）；
        /// 守村队留够守村的（一半、不少于 5 人，<see cref="SpareGuard"/>），祭司格子里已经占下的人也算在能走的数里。按部队空位封顶。
        /// </summary>
        internal static int DirectGuardRecruitsFor(Settlement settlement, MobileParty party) =>
            _instance == null || !party.IsLordParty || !GwpWardenRecruitment.IsWardenParty(party) ||
            !IsPreachedVillage(settlement) || !IsVillageQuiet(settlement) ||
            settlement.MilitiaPartyComponent?.MobileParty?.IsActive != true ||
            settlement.MilitiaPartyComponent.MobileParty.MapEvent != null
                ? 0
                : Math.Min(FreeSlots(party), _instance.DirectGuardRecruitsAvailable(_instance._villages[settlement.StringId]));

        private int DirectGuardRecruitsAvailable(FaithVillage row)
        {
            int inSlots = FindLivingPriest(row)?.VolunteerTypes.Count(troop => troop != null) ?? 0;
            return Math.Max(0, SpareGuard(row) - inSlots);
        }

        // ---------------------------------------------------------------- 事件分派

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            GwpFireswornMenus.Register(starter);
            GwpFireswornPartyDialogs.Register(starter);
            RebuildPriestIndex();
            foreach (FaithVillage row in _villages.Values.ToList())
                ObserveGuard(row);
        }

        /// <summary>
        /// 开新档时灰袍的家乡村就是第一座信众村：照重建给 5% 的火誓者，再拉起一位祭司。
        /// 没有它，灰袍在第一次重建之前补不了兵：灰袍只能在信众村招兵（用户裁定）。
        /// </summary>
        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            Settlement home = PoliceStats.GetPoliceClan().InitialHomeSettlement;
            OnVillageRebuilt(home);
            FaithVillage row = _villages[home.StringId];
            // 新档的初始守村兵是开局配置；普通重建和传教不再即时产兵（用户裁定 2026-10-09）。
            AddToGuard(row, home, CharacterObject.Find(GwpIds.ZealotId), GuardHeadroom(row));
            InstallPriest(home);
        }

        private void OnHourlyTick()
        {
            // 只在本次结算内使用：避免每本军械账、每支物流队反复全图线性找队；不存档。
            _partySnapshot = MobileParty.All.ToDictionary(party => party.StringId, StringComparer.OrdinalIgnoreCase);
            try
            {
                UpdateRituals();
                ClearExpiredSmoke();
                TendFaithParties();
                SweepPrisonersAndSpoils();
                foreach (FaithVillage row in _villages.Values.ToList())
                    ObserveGuard(row);
                RefreshArmoryPlans();
                foreach (FaithVillage row in _villages.Values)
                    ReconcilePriestSlots(row);
            }
            finally { _partySnapshot = null; }
        }

        private void OnDailyTick()
        {
            // 只在本次结算内使用：避免每本军械账、每支物流队反复全图线性找队；不存档。
            _partySnapshot = MobileParty.All.ToDictionary(party => party.StringId, StringComparer.OrdinalIgnoreCase);
            try
            {
                MaintainPriests();
                GrowSworn();
                SpawnPriestsByShare();
                foreach (FaithVillage row in _villages.Values)
                    UpdatePriestPower(row);
                ScheduleCongregations();
                ScheduleDamnations();
                DispatchFaithParties();
                DispatchTrainingTransports();
                RebalanceArmories();
                WriteDailySummary();
            }
            finally { _partySnapshot = null; }
        }

        /// <summary>
        /// 原版每天给要人补格子、升格子里的兵（RecruitmentCampaignBehavior.DailyTickSettlement），
        /// 升出来的兵种守村队里可能没有；当天就照守村队重排，领主进村时格子里不会有凭空变出来的人。
        /// 原版那一步若排在这之后，每小时的重排会补上。
        /// </summary>
        private void OnDailyTickSettlement(Settlement settlement)
        {
            if (settlement.IsVillage && _villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                ReconcilePriestSlots(row);
        }

        /// <summary>
        /// 灰袍领主进信众村：交俘虏、捐东西、招兵、领装备。什么时候来由原版进城意愿决定（GwpWardenVisitDesire）。
        /// 原版进门招兵（祭司格子）已经在进门前跑完（RecruitmentCampaignBehavior.OnBeforeSettlementEntered），
        /// 招到的人由 <see cref="OnTroopRecruited"/> 从守村队带走，连同各自的那一套装备。
        /// </summary>
        private void OnSettlementEntered(MobileParty? party, Settlement settlement, Hero? hero)
        {
            if (party?.IsLordParty != true || !GwpWardenRecruitment.IsWardenParty(party) ||
                party.MapEvent != null || !IsPreachedVillage(settlement))
                return;
            HandOver(party, settlement);
            EnlistGuardRecruits(party, settlement);
        }

        private void OnSettlementLeft(MobileParty party, Settlement settlement) => _lastVisits.Remove(party.StringId);

        /// <summary>
        /// 祭司格子里的兵是守村队里的人（用户裁定 2026-10-07：从守村灰袍队里拉最精锐、配装最好的）。
        /// 领主招走了，守村队就少这几个，他们身上那一套也一起带走（用户裁定：招兵连装备带走）。
        /// </summary>
        private void OnTroopRecruited(Hero? recruiter, Settlement? settlement, Hero? source,
            CharacterObject troop, int count)
        {
            MobileParty? party = recruiter?.PartyBelongedTo;
            if (party == null || settlement == null || !GwpWardenRecruitment.IsWardenParty(party) ||
                !IsFireswornPriest(source) || !_villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                return;
            TroopRoster guard = GuardOf(row.VillageId);
            int taken = Math.Min(count, guard.GetTroopCount(troop));
            int kits = MoveBestSets(row.VillageId, party.StringId, troop, taken);
            if (taken > 0)
                guard.AddToCounts(troop, -taken);
            PushGuard(row, settlement);
            ReconcilePriestSlots(row);
            RecordVisit(party, settlement, recruits: count);
            GwpAiDiagnostics.WriteFaith("PRIEST_RECRUITED",
                "party=" + party.StringId + "; village=" + settlement.StringId + "; troop=" + troop.StringId +
                "; count=" + count + "; fromGuard=" + taken + "; kitItems=" + kits + "; guardLeft=" + guard.TotalManCount);
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            SettleBattleLosses(mapEvent);
            WriteBattleLoot(mapEvent);
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase? destroyer)
        {
            OnPartyGone(party, destroyer);
        }

        private static int FreeSlots(MobileParty party) =>
            Math.Max(0, party.Party.PartySizeLimit - party.Party.NumberOfAllMembers);

        private static int CountItems(ItemRoster roster)
        {
            int total = 0;
            for (int i = 0; i < roster.Count; i++)
                total += roster.GetElementNumber(i);
            return total;
        }

        private static void CreditTreasury(int amount) =>
            PoliceResourceManager.CreditJudicialTreasuryFromCourier(amount);

        // ---------------------------------------------------------------- 领主在信众村做了什么（地图状态文字）

        private sealed class VisitSummary
        {
            public string VillageId = string.Empty;
            public int Prisoners;
            public int Donated;
            public int Recruits;
        }

        // 领主这一次进信众村交了几个俘虏、捐了几件东西、招了几个兵，只给地图状态文字用，离村就清掉。
        // 不存档：读档后还在村里的领主显示通用的「在火堆边休整」，不影响任何行为。
        private readonly Dictionary<string, VisitSummary> _lastVisits =
            new Dictionary<string, VisitSummary>(StringComparer.OrdinalIgnoreCase);

        private void RecordVisit(MobileParty party, Settlement village, int prisoners = 0, int donated = 0, int recruits = 0)
        {
            if (!_lastVisits.TryGetValue(party.StringId, out VisitSummary? visit) ||
                !string.Equals(visit.VillageId, village.StringId, StringComparison.OrdinalIgnoreCase))
            {
                visit = new VisitSummary { VillageId = village.StringId };
                _lastVisits[party.StringId] = visit;
            }
            visit.Prisoners += prisoners;
            visit.Donated += donated;
            visit.Recruits += recruits;
        }

        /// <summary>地图上的状态文字（用户裁定 2026-10-07：领主在村里做什么用状态文字显示，不弹消息）。</summary>
        internal static bool TryDescribeActivity(MobileParty party, out TextObject text)
        {
            text = TextObject.GetEmpty();
            if (_instance == null)
                return false;
            if (_instance._faithParties.TryGetValue(party.StringId, out FaithParty? record))
            {
                Settlement home = Settlement.Find(record.HomeId);
                Settlement target = Settlement.Find(record.TargetId);
                if (record.Kind == FaithPartyKind.Training)
                {
                    text = record.Phase == FaithPartyPhase.Outbound
                        ? GwpText.Create(GwpTextCatalog.gwp_training_transport_outbound, "VILLAGE", home.Name)
                        : GwpText.Create(GwpTextCatalog.gwp_training_transport_settling, "VILLAGE", home.Name);
                    return true;
                }
                text = record.Phase == FaithPartyPhase.Outbound
                    ? record.Kind == FaithPartyKind.Mission
                        ? GwpText.Create(GwpTextCatalog.gwp_fs_status_mission, "TARGET", target.Name)
                        : record.Kind == FaithPartyKind.Balance
                            ? GwpText.Create(GwpTextCatalog.gwp_fs_status_balance,
                                "HOME", home.Name, "TARGET", target.Name)
                            : GwpText.Create(GwpTextCatalog.gwp_fs_status_market,
                                "HOME", home.Name, "TARGET", target.Name)
                    : record.Phase == FaithPartyPhase.Returning
                        ? GwpText.Create(GwpTextCatalog.gwp_fs_status_returning, "HOME", home.Name)
                        : GwpText.Create(GwpTextCatalog.gwp_fs_status_home, "HOME", home.Name);
                return true;
            }
            Settlement? village = party.CurrentSettlement;
            if (!party.IsLordParty || !GwpWardenRecruitment.IsWardenParty(party) || !IsPreachedVillage(village))
                return false;
            if (_instance._lastVisits.TryGetValue(party.StringId, out VisitSummary? visit) &&
                (visit.Prisoners > 0 || visit.Donated > 0 || visit.Recruits > 0))
                text = GwpText.Create(GwpTextCatalog.gwp_fs_status_visit,
                    "VILLAGE", village!.Name, "PRISONERS", visit.Prisoners, "DONATED", visit.Donated, "RECRUITS", visit.Recruits);
            else if (party.MemberRoster.TotalWounded > 0)
                text = GwpText.Create(GwpTextCatalog.gwp_fs_status_healing,
                    "VILLAGE", village!.Name);
            else
                text = GwpText.Create(GwpTextCatalog.gwp_fs_status_resting, "VILLAGE", village!.Name);
            return true;
        }

        // ---------------------------------------------------------------- 祭司

        /// <summary>
        /// 灰袍重建完一座村子：当地 5% 的人家信教，士兵按总缺额每日补入（用户裁定 2026-10-09）。
        /// 重建不拉祭司，祭司按比例生成之后才算信众村（<see cref="SpawnPriestsByShare"/>）。
        /// </summary>
        internal static void OnVillageRebuilt(Settlement village)
        {
            if (_instance == null)
                return;
            FaithVillage row = _instance.EnsureVillage(village);
            int before = row.Households;
            int added = Convert(row, village,
                (int)Math.Round(village.Village.Hearth * GwpTuning.Firesworn.ReconstructionSwornShare));
            GwpAiDiagnostics.WriteFaith("RECONSTRUCTION_SWORN",
                "village=" + village.StringId + "; added=" + added + "; households=" + before + "->" + row.Households +
                "; guard=" + _instance.Sworn(row) + "; hearth=" + village.Village.Hearth.ToString("0"));
        }

        private void InstallPriest(Settlement village)
        {
            FaithVillage row = EnsureVillage(village);
            if (FindLivingPriest(row) != null)
                return;

            // 祭司是男人（用户裁定 2026-10-07：男性负责传教相关，女性负责大局统领）。原版每个文化有一到两个男性传教者模板
            // （spc_notable_<文化>_N，C#反编译 v1.4.8 spspecialcharacters.xml），随机挑一个，长相在它的捏脸范围里随机。
            List<CharacterObject> templates = CharacterObject.All.Where(character =>
                character.IsTemplate && character.Occupation == Occupation.Preacher &&
                !character.IsFemale && character.Culture == village.Culture).ToList();
            CharacterObject template = templates[MBRandom.RandomInt(templates.Count)];

            Hero priest = HeroCreator.CreateSpecialHero(template, village, null, null,
                MBRandom.RandomInt(35, 56));
            priest.SetNewOccupation(Occupation.Preacher);
            priest.BornSettlement = village;
            priest.UpdateHomeSettlement();
            if (!priest.IsActive)
                priest.ChangeState(Hero.CharacterStates.Active);
            if (priest.CurrentSettlement != village)
                EnterSettlementAction.ApplyForCharacterOnly(priest, village);
            priest.SupporterOf = PoliceStats.GetPoliceClan();
            if (priest.Gold < GwpTuning.Firesworn.PriestStartingGold)
                GiveGoldAction.ApplyBetweenCharacters(null, priest,
                    GwpTuning.Firesworn.PriestStartingGold - priest.Gold, true);
            AnointPriest(row, priest, village);
            UpdatePriestPower(row);
            // 一座村子成了信众村，是重要的事，玩家随时都该知道（用户裁定 2026-10-07：灰色消息只给重要的事）。
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                GwpTextCatalog.gwp_fs_priest_arrived,
                "PRIEST", priest.Name, "VILLAGE", village.Name), Colors.Gray));
        }

        /// <summary>
        /// 让一位男性要人成为这座村子的祭司：加祭司称号、穿袍子、写教义、登记。
        /// 名字用原版建人时从本文化男名池取的那个（C#反编译 v1.4.8 NameGenerator.GenerateHeroFirstName：
        /// 按全局使用次数和同村要人避开重名），不另设名池（用户裁定 2026-10-14：祭司重名带序号）。
        /// </summary>
        private void AnointPriest(FaithVillage row, Hero priest, Settlement village)
        {
            TextObject firstName = priest.FirstName;
            priest.SetName(GwpText.Create(GwpTextCatalog.gwp_fs_priest_name,
                "FIRSTNAME", firstName), firstName);
            priest.SetTraitLevel(DefaultTraits.Mercy, -1);
            priest.EncyclopediaText = GwpText.Create(
                GwpTextCatalog.gwp_fs_priest_lore,
                "PRIEST", firstName, "VILLAGE", village.Name);
            DressPriest(priest);
            row.PriestId = priest.StringId;
            _priestIds.Add(priest.StringId);
        }

        private static void DressPriest(Hero priest)
        {
            ItemObject robe = MBObjectManager.Instance.GetObject<ItemObject>("monk_robe");
            ItemObject hood = MBObjectManager.Instance.GetObject<ItemObject>("pilgrim_hood");
            foreach (Equipment equipment in new[] { priest.CivilianEquipment, priest.BattleEquipment })
            {
                equipment[EquipmentIndex.Body] = new EquipmentElement(robe);
                equipment[EquipmentIndex.Head] = new EquipmentElement(hood);
            }
        }

        /// <summary>
        /// 祭司死时 power 够高，原版会给村子补一位同职业的亲属（<c>NotablesCampaignBehavior.OnHeroKilled</c>
        /// → <c>HeroCreator.CreateRelativeNotableHero</c>，模板从该文化的传教者里随机，男女都有）。
        /// 补来的是男人就接任祭司；是女人就只是原版传教者，不接火（用户裁定 2026-10-07：祭司是男人）。
        /// 没人接任，这座村就不再是信众村，等按比例再来一位。
        /// </summary>
        private void MaintainPriests()
        {
            foreach (FaithVillage row in _villages.Values)
            {
                if (string.IsNullOrEmpty(row.PriestId) || FindLivingPriest(row) != null)
                    continue;
                Settlement village = Settlement.Find(row.VillageId);
                _priestIds.Remove(row.PriestId);
                row.PriestId = string.Empty;
                Hero? successor = village.Notables.FirstOrDefault(hero =>
                    hero.IsPreacher && !hero.IsFemale && hero.IsAlive && !_priestIds.Contains(hero.StringId));
                // 这一带的信众村已经够数：不接任，这座村照原版回到普通村子（软上限，用户裁定 2026-10-15）。
                if (successor != null && !FaithRegionHasRoom(village, GwpPatrolBeats.Assignments()))
                {
                    GwpAiDiagnostics.WriteFaith("PRIEST_SUCCESSION_HELD", "village=" + village.StringId +
                        "; successor=" + successor.StringId);
                    continue;
                }
                if (successor != null)
                {
                    AnointPriest(row, successor, village);
                    UpdatePriestPower(row);
                }
            }
        }

        private static Hero? FindLivingPriest(FaithVillage row)
        {
            if (string.IsNullOrEmpty(row.PriestId))
                return null;
            Hero? hero = Hero.Find(row.PriestId);
            return hero?.IsAlive == true ? hero : null;
        }

        private void RebuildPriestIndex()
        {
            _priestIds.Clear();
            foreach (FaithVillage row in _villages.Values)
                if (!string.IsNullOrEmpty(row.PriestId))
                    _priestIds.Add(row.PriestId);
        }

        /// <summary>
        /// 祭司由要人系统按火誓者比例自动生成（用户裁定）。照原版补要人的节奏：每座村每 7 天查一次
        /// （C#反编译 v1.4.8 NotablesCampaignBehavior.DailyTickSettlement → SettlementHelper.SpawnNotablesIfNeeded），
        /// 哪一天查由村子决定。没有祭司、火誓者占户数到了门槛，就按概率来一位。每村只有一位祭司（用户裁定）。
        /// 祭司来了这座村才算信众村，解锁烧人和传教（用户裁定 2026-10-07）。
        /// </summary>
        private void SpawnPriestsByShare()
        {
            int dayOfWeek = (int)CampaignTime.Now.ToDays % 7;
            Dictionary<Settlement, int> beats = GwpPatrolBeats.Assignments();
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                if (StableDay(row.VillageId, 7) != dayOfWeek || FindLivingPriest(row) != null)
                    continue;
                Settlement village = Settlement.Find(row.VillageId);
                float share = SwornShare(row, village);
                if (share < GwpTuning.Firesworn.PriestZealotShare ||
                    !FaithRegionHasRoom(village, beats) ||
                    MBRandom.RandomFloat >= GwpTuning.Firesworn.PriestWeeklyChance)
                    continue;
                InstallPriest(village);
                GwpAiDiagnostics.WriteFaith("PRIEST_SPAWNED",
                    "village=" + village.StringId + "; households=" + row.Households + "; guard=" + Sworn(row) +
                    "; hearth=" + village.Village.Hearth.ToString("0") + "; share=" + share.ToString("0.00"));
            }
        }

        /// <summary>
        /// 祭司的势力按当地火誓者比例算：祭司占全村要人势力的份额等于火誓者占户数的份额，
        /// 其他要人的势力照原版不动（用户裁定 2026-10-07）。所以 祭司 = 其他要人势力之和 × 比例 / (1 − 比例)。
        /// 原版要人势力：普通 &lt; 100、有影响力 100–200、强大 &gt; 200，初始 50–400 随机，每天只变零点几
        /// （C#反编译 v1.4.8 DefaultNotablePowerModel）。
        /// </summary>
        /// <summary>
        /// 祭司势力：全村要人势力总量不变，祭司拿火誓者比例那一份，其他要人按各自现有势力分剩下的（用户裁定 2026-10-07）。
        /// 早先按「其他要人之和 × 比例 /（1 − 比例）」只抬祭司，比例接近 1 时祭司接近无穷大。
        /// 其他要人会因此掉到 100 以下，原版每天按 (100 − 势力)/100 × 2% 的概率删掉他们（C#反编译 v1.4.8
        /// DefaultNotablePowerModel、NotablesCampaignBehavior），用户裁定时知情。村里没有别的要人时不动。
        /// </summary>
        /// <summary>这座村归哪个巡区：离它最近的在用巡区圆心。</summary>
        private static Settlement? FaithRegionOf(Settlement village, Dictionary<Settlement, int> beats) =>
            beats.Keys.OrderBy(center => center.GetPosition2D.DistanceSquared(village.GetPosition2D)).FirstOrDefault();

        /// <summary>
        /// 这座村所在巡区的信众村（有活祭司的村）满了没有。每座巡区容纳 <c>FaithVillagesPerWarden</c> × 守它的领主数，
        /// 合起来就是全图 3 × 在编领主数（用户裁定 2026-10-15：信众村尽量长在灰袍的巡逻点一带，全图上限是灰袍领主数的三倍）。
        /// 软上限：只挡新来祭司和祭司接任，已有的信众村不拆，照原版退休、死亡后自然回到普通村子。
        /// </summary>
        private bool FaithRegionHasRoom(Settlement village, Dictionary<Settlement, int> beats) =>
            FaithRegionLoad(village, beats) < 1f;

        /// <summary>这座村所在巡区已用掉的比例（不算它自己）；没有巡区时为 1（满）。</summary>
        private float FaithRegionLoad(Settlement village, Dictionary<Settlement, int> beats)
        {
            Settlement? region = FaithRegionOf(village, beats);
            if (region == null) return 1f;
            int sworn = _villages.Values.Count(row =>
                !string.Equals(row.VillageId, village.StringId, StringComparison.OrdinalIgnoreCase) &&
                FindLivingPriest(row) != null &&
                FaithRegionOf(Settlement.Find(row.VillageId), beats) == region);
            return sworn / (float)(beats[region] * GwpTuning.Firesworn.FaithVillagesPerWarden);
        }

        private void UpdatePriestPower(FaithVillage row)
        {
            Hero? priest = FindLivingPriest(row);
            if (priest == null)
                return;
            Settlement village = Settlement.Find(row.VillageId);
            List<Hero> others = OtherNotables(village, priest).ToList();
            float othersPower = others.Sum(hero => hero.Power);
            if (othersPower <= 0f)
                return;
            float total = othersPower + priest.Power;
            float share = Math.Min(SwornShare(row, village), GwpTuning.Firesworn.PriestMaxPowerShare);
            float priestPower = total * share;
            float scale = (total - priestPower) / othersPower;
            // 需要严格领先时，允许祭司略超一半，但全部差额必须来自其他要人，
            // 不再每次重算凭空扣掉1点总势力（用户裁定2026-10-16）。
            if (share >= GwpTuning.Firesworn.PriestMaxPowerShare)
            {
                float strongest = others.Max(hero => hero.Power);
                priestPower = Math.Max(priestPower, Math.Min(total,
                    (total * strongest + othersPower) / (othersPower + strongest)));
                scale = (total - priestPower) / othersPower;
            }
            foreach (Hero other in others)
                other.AddPower(other.Power * scale - other.Power);
            priest.AddPower(priestPower - priest.Power);
        }

        private static IEnumerable<Hero> OtherNotables(Settlement village, Hero priest) =>
            village.Notables.Where(hero => hero != priest && hero.IsAlive);

        /// <summary>
        /// 祭司已是本地最强（有祭司且势力高过村里每一位别的要人）。从这时起本村能烧自己人、不再出新的原版民兵
        /// （用户裁定 2026-10-07）。
        /// </summary>
        internal static bool IsPriestStrongestAt(Settlement? settlement) =>
            settlement?.IsVillage == true && _instance != null &&
            _instance._villages.TryGetValue(settlement.StringId, out FaithVillage? row) && IsPriestStrongest(row, settlement);

        /// <summary>本地要人里势力最强的是灰袍宗教：祭司的势力高过村里每一位别的要人（用户裁定 2026-10-07）。</summary>
        private static bool IsPriestStrongest(FaithVillage row, Settlement village)
        {
            Hero? priest = FindLivingPriest(row);
            return priest != null && OtherNotables(village, priest).All(hero => hero.Power < priest.Power);
        }

        /// <summary>
        /// 火誓者每天怎么长（用户裁定 2026-10-07：前期只靠烧俘虏和本村传教太慢；挂户数；灰袍所到之处促进增长）：
        /// <list type="bullet">
        /// <item>灰袍所到之处：灰袍领主这一天在哪座村里或村口（原版「正在这里」），那座村多两户信众。</item>
        /// <item>挂户数：原版这座村今天新增的户里，按火誓者比例那部分是信众户
        /// （C#反编译 v1.4.8 DefaultSettlementProsperityModel.CalculateHearthChange：不到 300 户每天 +4、不到 600 户 +1.2、再多 +0.2）。</item>
        /// <item>补兵：两户信众一个兵，当前人口包括本村所有无人队中的灰袍兵；只按总缺额每天5%补入狂信者，
        /// 与信众户增长分开（用户裁定 2026-10-09）。
        /// 不挂原版民兵增长：原版民兵 0.5 + 户数/400 − 现有 × 2.5%，民兵到了平衡点每天几乎是 0，守村队就跟着不补
        /// （C#反编译 v1.4.8 DefaultSettlementMilitiaModel；用户反馈 2026-10-07：民兵满了，守村队还差很远）。</item>
        /// </list>
        /// 原版的户数模型只读结果，不改。
        /// </summary>
        private void GrowSworn()
        {
            Clan police = PoliceStats.GetPoliceClan();
            CharacterObject zealot = CharacterObject.Find(GwpIds.ZealotId);
            Dictionary<string, int> presence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties()
                         .Where(party => party.IsActive && party.IsLordParty && party.MapEvent == null).ToList())
            {
                Settlement? here = MobilePartyHelper.GetCurrentSettlementOfMobilePartyForAICalculation(lord);
                if (here?.IsVillage != true || !IsVillageQuiet(here) || FactionManager.IsAtWarAgainstFaction(police, here.MapFaction))
                    continue;
                int added = Convert(EnsureVillage(here), here, GwpTuning.Firesworn.WardenPresenceDailySworn);
                presence.TryGetValue(here.StringId, out int already);
                presence[here.StringId] = already + added;
            }

            foreach (FaithVillage row in _villages.Values.ToList())
            {
                Settlement village = Settlement.Find(row.VillageId);
                float share = SwornShare(row, village);
                int fromHearth = 0, regrown = 0;
                if (share > 0f && IsVillageQuiet(village))
                {
                    float hearthGrowth = Campaign.Current.Models.SettlementProsperityModel.CalculateHearthChange(village.Village).ResultNumber;
                    // 全村信教以后新增的户全是信众（用户裁定 2026-10-14）：最近这次长户之前已经满了的村子直接补满，
                    // 不再按比例随机取整，免得户数每跨过一个整数就冒出一户不信教的。
                    if (hearthGrowth > 0f && row.Households >= (int)(village.Village.Hearth - hearthGrowth))
                        fromHearth = Convert(row, village, HouseholdCap(village) - row.Households);
                    else if (hearthGrowth > 0f)
                        fromHearth = Convert(row, village, MBRandom.RoundRandomized(hearthGrowth * share));
                }
                int headroom = GuardHeadroom(row);
                // 祭司最强以后原版民兵不再长，守村队照原版民兵的速度长（用户裁定 2026-10-15）；之前按缺额的 5% 补（用户裁定 2026-10-09）。
                // 两种都只补到两户一兵的上限。
                float dailyGrowth = IsPriestStrongest(row, village)
                    ? ((GwpFireswornMilitiaModel)Campaign.Current.Models.SettlementMilitiaModel).VanillaGrowth(village)
                    : headroom * GwpTuning.Firesworn.GuardRegrowDailyChance;
                if (IsVillageQuiet(village))
                    regrown = AddToGuard(row, village, zealot, MBRandom.RoundRandomized(dailyGrowth));
                presence.TryGetValue(row.VillageId, out int fromWardens);
                if (fromHearth + regrown + fromWardens <= 0)
                    continue;
                UpdatePriestPower(row);
                GwpAiDiagnostics.WriteFaith("SWORN_GROWTH",
                    "village=" + row.VillageId + "; fromWardensHouseholds=" + fromWardens + "; fromHearthHouseholds=" + fromHearth +
                    "; regrown=" + regrown + "; dailyGrowth=" + dailyGrowth.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                    "; households=" + row.Households + "; guard=" + Sworn(row) +
                    "; population=" + GuardPopulation(row) + "; cap=" + GuardCap(row) + "; headroomBefore=" + headroom +
                    "; share=" + SwornShare(row, village).ToString("0.000") + "; hearth=" + village.Village.Hearth.ToString("0"));
            }
        }

        /// <summary>按村子 id 给一个固定的日子（0…period−1），各村错开，不在同一天扎堆。</summary>
        /// <summary>
        /// 照比武大会开（用户裁定 2026-10-07）：本村每季固定的那一周、离上次同一件事（<paramref name="last"/>）至少 15 天，
        /// 每天固定概率。原版比武大会每座城每天判一次，上次开赛不到 15 天不开，每季 3 周里只有本城那一周能开；
        /// 原版的概率看在场的领主和英雄，这里用户定为固定概率（C#反编译 v1.4.8 TournamentCampaignBehavior.ConsiderStartOrEndTournament、
        /// DefaultTournamentModel.GetTournamentStartChance）。运械队、传教队走这里，每天调一次；本村传教、烧人见 <see cref="IsRitualDay"/>。
        /// </summary>
        private static bool IsFestivalDay(FaithVillage row, double last, double now, float chanceMultiplier = 1f) =>
            CampaignTime.Now.GetDayOfSeason / CampaignTime.DaysInWeek == StableDay(row.VillageId, CampaignTime.WeeksInSeason) &&
            now - last >= GwpTuning.Firesworn.FestivalIntervalDays * 24d &&
            MBRandom.RandomFloat < GwpTuning.Firesworn.FestivalDailyChance * chanceMultiplier;

        /// <summary>正在进行的仪式：村子 id 和哪一种（<see cref="GwpRitualMapSoundView"/> 用）。</summary>
        internal static IEnumerable<KeyValuePair<string, RitualKind>> ActiveRituals() =>
            _instance?._rituals.Select(ritual => new KeyValuePair<string, RitualKind>(ritual.VillageId, ritual.Kind)) ??
            Enumerable.Empty<KeyValuePair<string, RitualKind>>();

        /// <summary>
        /// 本村传教、烧人：离上次同一件事至少 GwpTuning.Firesworn.RitualIntervalDays，每天固定概率；两件事各算各的，
        /// 不限本村那一周，比比武大会勤一点（用户裁定 2026-10-07）。每天调一次。
        /// </summary>
        private static bool IsRitualDay(double last, double now) =>
            now - last >= GwpTuning.Firesworn.RitualIntervalDays * 24d &&
            MBRandom.RandomFloat < GwpTuning.Firesworn.RitualDailyChance;

        /// <summary>这座村正在烧人，地图上冒烟，和烧人锁村一样长（用户裁定 2026-10-07）（<see cref="GwpDamnationSmokePatch"/>）。</summary>
        internal static bool IsSmoking(Settlement settlement) =>
            _instance != null && _instance._villages.TryGetValue(settlement.StringId, out FaithVillage? row) &&
            CampaignTime.Now.ToHours - row.LastDamnation < GwpTuning.Firesworn.DamnationLockHours;

        private static int StableDay(string id, int period)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in id)
                    hash = hash * 31 + c;
                return Math.Abs(hash % period);
            }
        }

        // ---------------------------------------------------------------- 守村队

        private FaithVillage EnsureVillage(Settlement village)
        {
            if (_villages.TryGetValue(village.StringId, out FaithVillage? row))
                return row;
            // 原有民兵不转化（用户裁定 2026-10-07）：火誓者只靠重建、传教、烧人来。
            row = new FaithVillage { VillageId = village.StringId };
            _villages[village.StringId] = row;
            return row;
        }

        private TroopRoster GuardOf(string villageId) => RosterOf(_guards, villageId);

        /// <summary>
        /// 守村队自己长的上限：两户信众出一个兵（用户裁定 2026-10-08：原来一户一个，比例到顶时守村队约等于户数，
        /// 是原版民兵平衡点 20 + 户数/10 的约 5 倍；改成约 3 倍，仍和信众户挂钩。C#反编译 v1.4.8 DefaultSettlementMilitiaModel）。
        /// </summary>
        private static int GuardCap(FaithVillage row) => row.Households / GwpTuning.Firesworn.HouseholdsPerGuard;

        /// <summary>守村队人数。</summary>
        private int Sworn(FaithVillage row) =>
            _guards.TryGetValue(row.VillageId, out TroopRoster? guard) ? guard.TotalManCount : 0;

        /// <summary>
        /// 本村现有灰袍兵：在村守村队，加本村所有无人队中的灰袍兵（含伤兵、返程和停在村内的卖货队）。
        /// 出门不空出补兵名额；传教、运械到了别村并入后才转为那座村的兵。领主招走的不再算本村。
        /// （用户裁定 2026-10-09：增长按包括外出无人队在内的总人数与上限的差值计算。）
        /// </summary>
        private int GuardPopulation(FaithVillage row) => Sworn(row) + _faithParties.Values
            .Where(entry => entry.Kind != FaithPartyKind.Training &&
                string.Equals(entry.HomeId, row.VillageId, StringComparison.OrdinalIgnoreCase))
            .Select(entry => FindParty(entry.PartyId))
            .Where(party => party?.IsActive == true)
            .Sum(party => party!.MemberRoster.GetTroopRoster()
                .Where(element => IsGuardTroop(element.Character)).Sum(element => element.Number));

        private int GuardHeadroom(FaithVillage row) => Math.Max(0, GuardCap(row) - GuardPopulation(row));

        /// <summary>火誓者比例 = 信众户 ÷ 户数。祭司势力、来不来祭司、传教去哪儿都看它。</summary>
        private static float SwornShare(FaithVillage row, Settlement village) =>
            row.Households / Math.Max(1f, village.Village.Hearth);

        /// <summary>
        /// 信众户增长与士兵补入分开：这里只增加信众户，返回实际新增户数。
        /// 士兵统一按总缺额每日补入（用户裁定 2026-10-09）。
        /// </summary>
        private static int Convert(FaithVillage row, Settlement village, int count)
        {
            if (count <= 0)
                return 0;
            int before = row.Households;
            row.Households = Math.Min(HouseholdCap(village), before + count);
            return row.Households - before;
        }

        /// <summary>
        /// 按包括本村无人队在内的总缺额补兵，长到 <see cref="GuardCap"/> 为止。返回实际加了几个。
        /// </summary>
        private int AddToGuard(FaithVillage row, Settlement village, CharacterObject troop, int count) =>
            JoinGuard(row, village, troop, Math.Min(count, GuardHeadroom(row)));

        /// <summary>
        /// 人来了就收进守村队：烧人生出来的、传教队和运械队到了的、出门折回的，可以超过 <see cref="GuardCap"/>
        /// （用户裁定 2026-10-08：超过上限只是不再自己长）。返回加了几个。
        /// </summary>
        private int JoinGuard(FaithVillage row, Settlement village, CharacterObject troop, int count,
            TroopRoster? source = null)
        {
            if (count <= 0)
                return 0;
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            bool transferred = source != null && militia?.IsActive == true;
            if (transferred)
                count = GwpTroopTransfer.Move(source!, militia!.MemberRoster, troop, count).Number;
            GuardOf(row.VillageId).AddToCounts(troop, count);
            // 民兵队正在打仗（比如村子被劫掠）：PushGuard 不碰它，按记录补齐会把战死的人补回来；新来的人直接加进民兵队，
            // 原版纯 AI 的战斗每一轮模拟前按名单重新排兵（C#反编译 v1.4.8 MapEventSide.MakeReadyForSimulation → MapEventParty.Update），
            // 下一轮就上场；打完 ObserveGuard 照幸存的人记（用户裁定 2026-10-08：回来的人直接并入守村队，人数加进去一起打）。
            if (!transferred)
            {
                if (militia?.IsActive == true && militia.MapEvent != null)
                    militia.MemberRoster.AddToCounts(troop, count);
                else
                {
                    PushGuard(row, village);
                    militia = village.MilitiaPartyComponent?.MobileParty;
                    if (source != null && militia?.IsActive == true)
                    {
                        // 原版刚创建的民兵队由 PushGuard 填了人数；换入归队兵的真实状态。
                        militia.MemberRoster.AddToCounts(troop, -count);
                        GwpTroopTransfer.Move(source, militia.MemberRoster, troop, count);
                    }
                }
            }
            _plans.Remove(row.VillageId);
            return count;
        }

        internal static bool IsGuardTroop(CharacterObject? troop) =>
            troop != null && !troop.IsHero && GwpCommon.IsGreyWardenTroop(troop);

        /// <summary>
        /// 每小时从民兵队读回守村队：民兵队里的灰袍兵就是守村队现在的样子。
        /// <list type="bullet">
        /// <item>战斗减员、被俘都是真死；原版每天、每场战斗后也会给民兵队里攒够经验的兵升级（C#反编译 v1.4.8
        /// PartyUpgraderCampaignBehavior.DailyTickParty / MapEventEnded，原版村庄民兵没有升级路线，我们的灰袍兵有）。
        /// 所以只要民兵队里的灰袍兵不比记录多，就整份照民兵队记——升了级的照升级后的记（用户反馈 2026-10-08：
        /// 原来只认「比记录少」，升级的兵被当成战死，升出来的又被当成多余删掉）。</item>
        /// <item>民兵队没了，守村队也没了（信众户还在，慢慢补回）。</item>
        /// <item>户数掉了，信众户压到户数。守村队超过 <see cref="GuardCap"/> 不删，只是不再自己长。</item>
        /// </list>
        /// 然后把民兵队补齐到记录。
        /// </summary>
        private void ObserveGuard(FaithVillage row)
        {
            Settlement? village = Settlement.Find(row.VillageId);
            if (village?.IsVillage != true)
                return;
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.MapEvent != null)
                return;
            TroopRoster guard = GuardOf(row.VillageId);
            List<TroopRosterElement> observed = militia?.IsActive == true
                ? militia.MemberRoster.GetTroopRoster().Where(element => IsGuardTroop(element.Character)).ToList()
                : new List<TroopRosterElement>();
            int observedTotal = observed.Sum(element => element.Number);
            bool differs = observedTotal != guard.TotalManCount ||
                           observed.Any(element => element.Number != guard.GetTroopCount(element.Character));
            // 比记录多：刚加进记录的还没推进民兵队，下面 PushGuard 会补齐、去掉多的。
            if (differs && observedTotal <= guard.TotalManCount)
            {
                guard.Clear();
                foreach (TroopRosterElement element in observed)
                    guard.AddToCounts(element.Character, element.Number);
                _plans.Remove(row.VillageId);
            }
            row.Households = Math.Min(row.Households, HouseholdCap(village));
            PushGuard(row, village);
        }

        /// <summary>
        /// 把守村队放进民兵队，和原版民兵站在一起：原版民兵一个不动，灰袍兵按记录补齐、多的去掉。
        /// 第一个灰袍兵抵达时就创建真实民兵队，不等待原版增长累计超过1的阈值。
        /// </summary>
        private void PushGuard(FaithVillage row, Settlement village)
        {
            TroopRoster guard = GuardOf(row.VillageId);
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.MapEvent != null)
                return;
            if (militia?.IsActive != true)
            {
                if (guard.TotalManCount == 0)
                    return;
                militia = EnsureVillageMilitia(village);
            }

            TroopRoster roster = militia.MemberRoster;
            foreach (TroopRosterElement element in roster.GetTroopRoster().ToList())
                if (IsGuardTroop(element.Character) && guard.GetTroopCount(element.Character) == 0)
                    roster.AddToCounts(element.Character, -element.Number, false, -element.WoundedNumber);
            foreach (TroopRosterElement element in guard.GetTroopRoster())
            {
                int present = roster.GetTroopCount(element.Character);
                if (present < element.Number)
                {
                    roster.AddToCounts(element.Character, element.Number - present);
                }
                else if (present > element.Number)
                {
                    int remove = present - element.Number;
                    int index = roster.FindIndexOfTroop(element.Character);
                    int healthy = roster.GetElementNumber(index) - roster.GetElementWoundedNumber(index);
                    roster.AddToCounts(element.Character, -remove, false, -Math.Max(0, remove - healthy));
                }
            }
        }

        private static MobileParty EnsureVillageMilitia(Settlement village)
        {
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.IsActive == true)
                return militia;
            // 原版公开工厂负责聚落挂接、停驻和存档；文化模板自带的占位兵
            // 不是实有村民，清空后由实际转入的名册填充，不能额外造兵。
            militia = MilitiaPartyComponent.CreateMilitiaParty(village.StringId, village);
            militia.MemberRoster.Clear();
            return militia;
        }

        /// <summary>按兵种比例从民兵队里拿掉 <paramref name="count"/> 个原版民兵，灰袍兵不动。</summary>
        internal static void RemoveVanillaMilitia(MobileParty militia, int count)
        {
            TroopRoster roster = militia.MemberRoster;
            List<TroopRosterElement> vanilla = roster.GetTroopRoster().Where(element => !IsGuardTroop(element.Character)).ToList();
            int total = vanilla.Sum(element => element.Number);
            if (total <= 0 || count <= 0)
                return;
            float share = Math.Min(1f, count / (float)total);
            int left = count;
            foreach (TroopRosterElement element in vanilla)
            {
                int take = Math.Min(left, Math.Min(element.Number, MBRandom.RoundRandomized(element.Number * share)));
                if (take <= 0)
                    continue;
                int healthy = element.Number - element.WoundedNumber;
                roster.AddToCounts(element.Character, -take, false, -Math.Max(0, take - healthy));
                left -= take;
            }
        }

        /// <summary>民兵队里有多少是守村队的灰袍兵。</summary>
        internal static int GuardHeadcount(Settlement? settlement) =>
            settlement != null && _instance != null && _instance._guards.TryGetValue(settlement.StringId, out TroopRoster? guard)
                ? guard.TotalManCount
                : 0;

        // ---------------------------------------------------------------- 祭司格子

        /// <summary>
        /// 祭司格子照原版：几个格子有人、什么时候补，由原版每天按概率决定（C#反编译 v1.4.8
        /// RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement）。格子里是谁由守村队决定：
        /// 有人的格子依次放守村队里最精锐的兵（等级高的在前，同兵种里配装好的先走），守村队没这么多人就空着；
        /// 守村队至少留一半（不少于 5 人）守村，格子只能放多出来的（实机诊断 2026-10-07：开局领主补兵把守村队从 11 人招到 5 人，
        /// 剩下的 5 人里 4 人又进了格子）；
        /// 格子按原版的顺序从低到高排（用户裁定 2026-10-07）。原版自己往上升格子里的兵会凭空变出守村队里没有的人，
        /// 所以每小时照守村队重排一次。
        /// </summary>
        private void ReconcilePriestSlots(FaithVillage row)
        {
            Hero? priest = FindLivingPriest(row);
            if (priest == null)
                return;
            int filled = Math.Min(priest.VolunteerTypes.Count(troop => troop != null), SpareGuard(row));
            List<CharacterObject> best = EliteOfGuard(row, filled)
                .OrderBy(troop => troop.Level + (troop.IsMounted ? 0.5f : 0f))
                .ToList();
            for (int i = 0; i < priest.VolunteerTypes.Length; i++)
                priest.VolunteerTypes[i] = i < best.Count ? best[i] : null;
        }

        /// <summary>守村队里最精锐的 <paramref name="count"/> 个人（只看兵种，同兵种里谁配装好由军械库的顺序决定）。</summary>
        private List<CharacterObject> EliteOfGuard(FaithVillage row, int count)
        {
            List<CharacterObject> elite = new List<CharacterObject>();
            foreach (TroopRosterElement element in GuardOf(row.VillageId).GetTroopRoster()
                         .OrderByDescending(entry => entry.Character.Tier)
                         .ThenByDescending(entry => entry.Character.IsMounted))
                for (int n = 0; n < element.Number && elite.Count < count; n++)
                    elite.Add(element.Character);
            return elite;
        }

        /// <summary>守村队里能让祭司格子拉走的人数：一半（不少于 5 人）留下守村。</summary>
        private int SpareGuard(FaithVillage row)
        {
            int sworn = Sworn(row);
            int keep = Math.Max(GwpTuning.Firesworn.MissionMinimumLeftBehind,
                (int)Math.Ceiling(sworn * GwpTuning.Firesworn.GuardKeptFromPriestSlots));
            return Math.Max(0, sworn - keep);
        }

        /// <summary>原版给空格子补人时问的「基础志愿兵」：祭司给守村队里最精锐的那个兵种，守村队没有多出来的人就不补。</summary>
        internal static CharacterObject? BestGuardTroop(Hero priest)
        {
            if (_instance == null)
                return null;
            FaithVillage? row = _instance._villages.Values.FirstOrDefault(entry =>
                string.Equals(entry.PriestId, priest.StringId, StringComparison.OrdinalIgnoreCase));
            return row == null || _instance.SpareGuard(row) <= 0 ? null : _instance.EliteOfGuard(row, 1).FirstOrDefault();
        }

        // ---------------------------------------------------------------- 工具

        private static bool IsVillageQuiet(Settlement settlement) =>
            settlement.Village?.VillageState == Village.VillageStates.Normal &&
            settlement.Party?.MapEvent == null;

        /// <summary>
        /// 到了这座聚落：在里面，或在村口附近。原版领主进村办完事马上出来、停在村口（实机诊断 2026-10-15），只认「在里面」永远等不到。
        /// </summary>
        internal static bool HasArrived(MobileParty party, Settlement settlement) =>
            party.CurrentSettlement == settlement ||
            party.GetPosition2D.Distance(settlement.GetPosition2D) <= GwpTuning.Firesworn.ArrivalDistance;

        private Dictionary<string, MobileParty>? _partySnapshot;
        private static MobileParty? FindParty(string partyId)
        {
            if (string.IsNullOrEmpty(partyId)) return null;
            if (_instance?._partySnapshot?.TryGetValue(partyId, out MobileParty party) == true)
                return party;
            return MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, partyId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
