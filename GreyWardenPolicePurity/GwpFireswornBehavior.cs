using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者：信众村、祭司、狂信者、地牢与烧人、传教队与狂信者队、军械库、灰袍领主的信众村差事
    /// （docs/state/firesworn.md、firesworn-economy.md）。
    ///
    /// 信众村在 <see cref="_villages"/> 里有一条记录；没有记录的村子一律走原版。
    /// 信众村的民兵完全由狂信者取代：民兵队里只有 <c>gwzealot</c>，人数就是狂信者数，
    /// 原版每日增减由 <see cref="GwpFireswornMilitiaModel"/> 归零，战斗减员从民兵队读回。
    ///
    /// 分文件：本文件是数据、存档、查询、事件分派、祭司与民兵；仪式见 GwpFireswornRituals.cs，
    /// 传教队与狂信者队见 GwpFireswornParties.cs，地牢、捐赠、领主差事见 GwpFireswornDungeon.cs，
    /// 军械库见 GwpFireswornArmory.cs。
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
        // 信众村地牢：灰袍领主交来的俘虏、守村俘获的敌兵，等着烧。
        private readonly Dictionary<string, TroopRoster> _dungeons =
            new Dictionary<string, TroopRoster>(StringComparer.OrdinalIgnoreCase);
        // 信众村的货仓：领主捐的、军械库用不完的东西，由狂信者队运进城卖。
        private readonly Dictionary<string, ItemRoster> _goods =
            new Dictionary<string, ItemRoster>(StringComparer.OrdinalIgnoreCase);
        // 军械库账本：键是信众村或灰袍领主部队的 StringId。兵身上穿的也算在里面。
        private readonly Dictionary<string, ItemRoster> _armories =
            new Dictionary<string, ItemRoster>(StringComparer.OrdinalIgnoreCase);
        // 每支灰袍领主部队已经扒过装备的俘虏数（按兵种）。俘虏多出来的才扒。
        private readonly Dictionary<string, Dictionary<string, int>> _stripped =
            new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Errand> _errands =
            new Dictionary<string, Errand>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _priestIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 祭司的名池与灰袍家族成员的分开，祭司之间不重名（GreyWardenFamilyBehavior.PickUniqueName）。
        private static readonly string[] PriestNames =
        {
            GwpText.Get("{=gwp_fs_priest_name_01}Aeledburh"),
            GwpText.Get("{=gwp_fs_priest_name_02}Aeledflaed"),
            GwpText.Get("{=gwp_fs_priest_name_03}Aeledwyn"),
            GwpText.Get("{=gwp_fs_priest_name_04}Brynehild"),
            GwpText.Get("{=gwp_fs_priest_name_05}Brynegifu"),
            GwpText.Get("{=gwp_fs_priest_name_06}Brynwyn"),
            GwpText.Get("{=gwp_fs_priest_name_07}Fyrburh"),
            GwpText.Get("{=gwp_fs_priest_name_08}Fyrgyth"),
            GwpText.Get("{=gwp_fs_priest_name_09}Fyrthryth"),
            GwpText.Get("{=gwp_fs_priest_name_10}Ligburh"),
            GwpText.Get("{=gwp_fs_priest_name_11}Ligflaed"),
            GwpText.Get("{=gwp_fs_priest_name_12}Liggifu"),
            GwpText.Get("{=gwp_fs_priest_name_13}Ligwyn"),
            GwpText.Get("{=gwp_fs_priest_name_14}Baelwyn"),
            GwpText.Get("{=gwp_fs_priest_name_15}Baelgyth"),
            GwpText.Get("{=gwp_fs_priest_name_16}Baelhild"),
            GwpText.Get("{=gwp_fs_priest_name_17}Ysleburh"),
            GwpText.Get("{=gwp_fs_priest_name_18}Yslewyn"),
            GwpText.Get("{=gwp_fs_priest_name_19}Gledburh"),
            GwpText.Get("{=gwp_fs_priest_name_20}Gledwyn"),
            GwpText.Get("{=gwp_fs_priest_name_21}Swelhild"),
            GwpText.Get("{=gwp_fs_priest_name_22}Brondgyth"),
            GwpText.Get("{=gwp_fs_priest_name_23}Brondwyn"),
            GwpText.Get("{=gwp_fs_priest_name_24}Heorthburh")
        };

        internal enum RitualKind
        {
            Congregation = 0,
            Damnation = 1
        }

        private enum FaithPartyKind
        {
            Disciples = 0,
            Caravan = 1
        }

        private enum FaithPartyPhase
        {
            Home = 0,
            Outbound = 1,
            Working = 2,
            Returning = 3
        }

        private sealed class FaithVillage
        {
            public string VillageId = string.Empty;
            public int Zealots;
            public double LastCongregation = Never;
            public double LastDamnation = Never;
            public double LastDisciple = Never;
            public double LastCaravan = Never;
            public string PriestId = string.Empty;
        }

        /// <summary>集会传教由传教队主持（HostPartyId）；烈火责罚由村子自己办，HostPartyId 为空。</summary>
        private sealed class Ritual
        {
            public string VillageId = string.Empty;
            public RitualKind Kind;
            public string HostPartyId = string.Empty;
            public bool Started;
            public double QueuedHour;
            public double EndHour;
        }

        /// <summary>每个信众村常驻的传教队、狂信者队，照原版村民队：回村待着，人没死光不销毁。</summary>
        private sealed class FaithParty
        {
            public string PartyId = string.Empty;
            public string HomeId = string.Empty;
            public FaithPartyKind Kind;
            public FaithPartyPhase Phase;
            public string TargetId = string.Empty;
            public double PhaseHour;
        }

        /// <summary>灰袍领主去信众村交俘虏、捐东西、招兵的差事。</summary>
        private sealed class Errand
        {
            public string VillageId = string.Empty;
            public double StartHour;
            public string Reason = string.Empty;
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
            CampaignEvents.SettlementEntered.AddNonSerializedListener(this, OnSettlementEntered);
            CampaignEvents.OnTroopRecruitedEvent.AddNonSerializedListener(this, OnTroopRecruited);
            CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
            CampaignEvents.MobilePartyDestroyed.AddNonSerializedListener(this, OnMobilePartyDestroyed);
            CampaignEvents.OnNewGameCreatedPartialFollowUpEndEvent.AddNonSerializedListener(this, OnNewGameCreated);
        }

        // ---------------------------------------------------------------- 存档

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? villageIds = null, priests = null;
            List<int>? zealots = null;
            List<double>? congregations = null, damnations = null, disciples = null, caravans = null;
            List<string>? ritualVillages = null, ritualHosts = null;
            List<int>? ritualKinds = null, ritualStarted = null;
            List<double>? ritualQueued = null, ritualEnds = null;
            List<string>? partyIds = null, partyHomes = null, partyTargets = null;
            List<int>? partyKinds = null, partyPhases = null;
            List<double>? partyHours = null;
            List<string>? errandParties = null, errandVillages = null, errandReasons = null;
            List<double>? errandHours = null;

            if (dataStore.IsSaving)
            {
                List<FaithVillage> rows = _villages.Values.ToList();
                villageIds = rows.Select(row => row.VillageId).ToList();
                zealots = rows.Select(row => row.Zealots).ToList();
                congregations = rows.Select(row => row.LastCongregation).ToList();
                damnations = rows.Select(row => row.LastDamnation).ToList();
                disciples = rows.Select(row => row.LastDisciple).ToList();
                caravans = rows.Select(row => row.LastCaravan).ToList();
                priests = rows.Select(row => row.PriestId).ToList();
                ritualVillages = _rituals.Select(ritual => ritual.VillageId).ToList();
                ritualKinds = _rituals.Select(ritual => (int)ritual.Kind).ToList();
                ritualHosts = _rituals.Select(ritual => ritual.HostPartyId).ToList();
                ritualStarted = _rituals.Select(ritual => ritual.Started ? 1 : 0).ToList();
                ritualQueued = _rituals.Select(ritual => ritual.QueuedHour).ToList();
                ritualEnds = _rituals.Select(ritual => ritual.EndHour).ToList();
                List<FaithParty> parties = _faithParties.Values.ToList();
                partyIds = parties.Select(party => party.PartyId).ToList();
                partyHomes = parties.Select(party => party.HomeId).ToList();
                partyKinds = parties.Select(party => (int)party.Kind).ToList();
                partyPhases = parties.Select(party => (int)party.Phase).ToList();
                partyTargets = parties.Select(party => party.TargetId).ToList();
                partyHours = parties.Select(party => party.PhaseHour).ToList();
                errandParties = _errands.Keys.ToList();
                errandVillages = errandParties.Select(id => _errands[id].VillageId).ToList();
                errandHours = errandParties.Select(id => _errands[id].StartHour).ToList();
                errandReasons = errandParties.Select(id => _errands[id].Reason).ToList();
            }

            dataStore.SyncData("GWPP_FsVillageIds", ref villageIds);
            dataStore.SyncData("GWPP_FsVillageZealots", ref zealots);
            dataStore.SyncData("GWPP_FsVillageLastCongregation", ref congregations);
            dataStore.SyncData("GWPP_FsVillageLastDamnation", ref damnations);
            dataStore.SyncData("GWPP_FsVillageLastDisciple", ref disciples);
            dataStore.SyncData("GWPP_FsVillageLastCaravan", ref caravans);
            dataStore.SyncData("GWPP_FsVillagePriest", ref priests);
            dataStore.SyncData("GWPP_FsRitualVillage", ref ritualVillages);
            dataStore.SyncData("GWPP_FsRitualKind", ref ritualKinds);
            dataStore.SyncData("GWPP_FsRitualHost", ref ritualHosts);
            dataStore.SyncData("GWPP_FsRitualStarted", ref ritualStarted);
            dataStore.SyncData("GWPP_FsRitualQueued", ref ritualQueued);
            dataStore.SyncData("GWPP_FsRitualEnd", ref ritualEnds);
            dataStore.SyncData("GWPP_FsPartyIds", ref partyIds);
            dataStore.SyncData("GWPP_FsPartyHomes", ref partyHomes);
            dataStore.SyncData("GWPP_FsPartyKinds", ref partyKinds);
            dataStore.SyncData("GWPP_FsPartyPhases", ref partyPhases);
            dataStore.SyncData("GWPP_FsPartyTargets", ref partyTargets);
            dataStore.SyncData("GWPP_FsPartyHours", ref partyHours);
            dataStore.SyncData("GWPP_FsErrandParties", ref errandParties);
            dataStore.SyncData("GWPP_FsErrandVillages", ref errandVillages);
            dataStore.SyncData("GWPP_FsErrandHours", ref errandHours);
            dataStore.SyncData("GWPP_FsErrandReasons", ref errandReasons);
            SyncTroopRosters(dataStore, "GWPP_FsDungeon", _dungeons);
            SyncItemRosters(dataStore, "GWPP_FsGoods", _goods);
            SyncItemRosters(dataStore, "GWPP_FsArmory", _armories);
            SyncStripped(dataStore);

            if (!dataStore.IsLoading)
                return;

            _villages.Clear();
            for (int i = 0; i < villageIds!.Count; i++)
                _villages[villageIds[i]] = new FaithVillage
                {
                    VillageId = villageIds[i],
                    Zealots = zealots![i],
                    LastCongregation = congregations![i],
                    LastDamnation = damnations![i],
                    LastDisciple = disciples![i],
                    LastCaravan = caravans![i],
                    PriestId = priests![i]
                };

            _rituals.Clear();
            for (int i = 0; i < ritualVillages!.Count; i++)
                _rituals.Add(new Ritual
                {
                    VillageId = ritualVillages[i],
                    Kind = (RitualKind)ritualKinds![i],
                    HostPartyId = ritualHosts![i],
                    Started = ritualStarted![i] != 0,
                    QueuedHour = ritualQueued![i],
                    EndHour = ritualEnds![i]
                });

            _faithParties.Clear();
            for (int i = 0; i < partyIds!.Count; i++)
                _faithParties[partyIds[i]] = new FaithParty
                {
                    PartyId = partyIds[i],
                    HomeId = partyHomes![i],
                    Kind = (FaithPartyKind)partyKinds![i],
                    Phase = (FaithPartyPhase)partyPhases![i],
                    TargetId = partyTargets![i],
                    PhaseHour = partyHours![i]
                };

            _errands.Clear();
            for (int i = 0; i < errandParties!.Count; i++)
                _errands[errandParties[i]] = new Errand
                {
                    VillageId = errandVillages![i],
                    StartHour = errandHours![i],
                    Reason = errandReasons![i]
                };

            RebuildPriestIndex();
        }

        private static void SyncTroopRosters(IDataStore dataStore, string key,
            Dictionary<string, TroopRoster> rosters)
        {
            List<string>? holders = null, characters = null;
            List<int>? counts = null;
            if (dataStore.IsSaving)
            {
                holders = new List<string>();
                characters = new List<string>();
                counts = new List<int>();
                foreach (KeyValuePair<string, TroopRoster> pair in rosters)
                    foreach (TroopRosterElement element in pair.Value.GetTroopRoster())
                    {
                        holders.Add(pair.Key);
                        characters.Add(element.Character.StringId);
                        counts.Add(element.Number);
                    }
            }
            dataStore.SyncData(key + "Holders", ref holders);
            dataStore.SyncData(key + "Characters", ref characters);
            dataStore.SyncData(key + "Counts", ref counts);
            if (!dataStore.IsLoading)
                return;
            rosters.Clear();
            for (int i = 0; i < holders!.Count; i++)
                RosterOf(rosters, holders[i]).AddToCounts(CharacterObject.Find(characters![i]), counts![i]);
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

        private void SyncStripped(IDataStore dataStore)
        {
            List<string>? parties = null, characters = null;
            List<int>? counts = null;
            if (dataStore.IsSaving)
            {
                parties = new List<string>();
                characters = new List<string>();
                counts = new List<int>();
                foreach (KeyValuePair<string, Dictionary<string, int>> party in _stripped)
                    foreach (KeyValuePair<string, int> entry in party.Value)
                    {
                        parties.Add(party.Key);
                        characters.Add(entry.Key);
                        counts.Add(entry.Value);
                    }
            }
            dataStore.SyncData("GWPP_FsStrippedParties", ref parties);
            dataStore.SyncData("GWPP_FsStrippedCharacters", ref characters);
            dataStore.SyncData("GWPP_FsStrippedCounts", ref counts);
            if (!dataStore.IsLoading)
                return;
            _stripped.Clear();
            for (int i = 0; i < parties!.Count; i++)
                StrippedOf(parties[i])[characters![i]] = counts![i];
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

        private Dictionary<string, int> StrippedOf(string partyId)
        {
            if (!_stripped.TryGetValue(partyId, out Dictionary<string, int>? counts))
            {
                counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                _stripped[partyId] = counts;
            }
            return counts;
        }

        // ---------------------------------------------------------------- 查询

        internal static bool IsPreachedVillage(Settlement? settlement) =>
            settlement?.IsVillage == true && _instance != null &&
            _instance._villages.ContainsKey(settlement.StringId);

        internal static bool IsFireswornPriest(Hero? hero) =>
            hero != null && _instance != null && _instance._priestIds.Contains(hero.StringId);

        internal static bool IsOnFaithErrand(MobileParty? party) =>
            party != null && _instance != null && _instance._errands.ContainsKey(party.StringId);

        /// <summary>案子发生在某座信众村跟前：灰袍优先接这种案子（用户裁定）。</summary>
        internal static bool IsNearFaithVillage(Vec2 position) =>
            _instance != null && _instance._villages.Keys.Any(id =>
                Settlement.Find(id).GetPosition2D.Distance(position) <= GwpTuning.Firesworn.FaithCaseRadius);

        internal static bool TryGetVillageFaith(Settlement? settlement, out int zealots,
            out int cap, out Hero? priest)
        {
            zealots = cap = 0;
            priest = null;
            if (settlement?.IsVillage != true || _instance == null ||
                !_instance._villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                return false;
            zealots = row.Zealots;
            cap = ZealotCap(settlement);
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

        /// <summary>村子正在举办、已经开始的仪式。期间玩家进村只看到仪式画面。</summary>
        internal static bool TryGetActiveRitual(Settlement? settlement, out RitualKind kind,
            out double remainingHours, out string hostName)
        {
            kind = RitualKind.Congregation;
            remainingHours = 0d;
            hostName = string.Empty;
            if (settlement?.IsVillage != true || _instance == null)
                return false;
            Ritual? ritual = _instance._rituals.FirstOrDefault(entry => entry.Started &&
                string.Equals(entry.VillageId, settlement.StringId, StringComparison.OrdinalIgnoreCase));
            if (ritual == null)
                return false;
            kind = ritual.Kind;
            remainingHours = Math.Max(0d, ritual.EndHour - CampaignTime.Now.ToHours);
            hostName = _instance.RitualHostName(ritual, settlement);
            return true;
        }

        /// <summary>狂信者上限就是户数，跟着户数变（用户裁定 2026-10-05）。</summary>
        internal static int ZealotCap(Settlement settlement) => (int)settlement.Village.Hearth;

        /// <summary>
        /// 灰袍领主进这座村能直接招多少个最低级狂信者。代价是户数，照原版村民队补人的算法：
        /// 招 n 人扣 (n+1)/2 户（C#反编译 v1.4.8 VillagerCampaignBehavior.AddVillagersToParty）。
        /// 户数不往 <see cref="GwpTuning.Firesworn.DirectZealotHearthFloor"/> 以下扣。
        /// </summary>
        internal static int DirectZealotsAvailable(Settlement settlement)
        {
            if (!IsPreachedVillage(settlement) || !IsVillageQuiet(settlement))
                return 0;
            float spareHearth = settlement.Village.Hearth - GwpTuning.Firesworn.DirectZealotHearthFloor;
            return Math.Max(0, (int)(spareHearth * 2f));
        }

        /// <summary>
        /// 给原版进村评分用：灰袍在这里能招到的兵 = 祭司格子里的兵 + 能直接招的狂信者，
        /// 后者按领主部队的空位封顶。不是信众村就是 0。
        /// </summary>
        internal static int RecruitsAvailableToWardens(Settlement settlement, Hero hero)
        {
            if (!IsPreachedVillage(settlement) || _instance == null)
                return 0;
            Hero? priest = FindLivingPriest(_instance._villages[settlement.StringId]);
            int priestSlots = priest?.VolunteerTypes.Count(troop => troop != null) ?? 0;
            MobileParty? party = hero.PartyBelongedTo;
            int freeSlots = party == null ? 0 : FreeSlots(party);
            return priestSlots + Math.Min(freeSlots, DirectZealotsAvailable(settlement));
        }

        // ---------------------------------------------------------------- 事件分派

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            GwpFireswornMenus.Register(starter);
            RebuildPriestIndex();
            foreach (FaithVillage row in _villages.Values.ToList())
                ObserveAndPushMilitia(row);
        }

        /// <summary>
        /// 开新档时灰袍的家乡村就是第一座信众村，有一位祭司。没有它，灰袍在第一次重建之前补不了兵：
        /// 灰袍只能在信众村招兵（用户裁定）。
        /// </summary>
        private void OnNewGameCreated(CampaignGameStarter starter)
        {
            InstallPriest(PoliceStats.GetPoliceClan().InitialHomeSettlement);
        }

        private void OnHourlyTick()
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_RITUALS", UpdateRituals);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PARTIES", TendFaithParties);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_SPOILS", SweepPrisonersAndSpoils);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_ERRANDS", TendErrands);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_MILITIA", () =>
            {
                foreach (FaithVillage row in _villages.Values.ToList())
                    ObserveAndPushMilitia(row);
            });
            GwpRuntimeFaultWatch.Guard("FIRESWORN_ARMORY", RefreshArmoryPlans);
        }

        private void OnDailyTick()
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PRIESTS", MaintainPriests);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PRIEST_TRAINING", TrainPriestVolunteers);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PRIEST_SPAWN", SpawnPriestsByShare);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_DAMNATIONS", ScheduleDamnations);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_DISCIPLES", DispatchDisciples);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_CARAVANS", DispatchCaravans);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_ARMORY_REBALANCE", RebalanceArmories);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_SUMMARY", WriteDailySummary);
        }

        /// <summary>
        /// 灰袍领主进信众村：交俘虏、捐东西、招兵、领装备，差事就此办完。原版进门招兵（祭司格子）
        /// 已经在进门前跑完（RecruitmentCampaignBehavior.OnBeforeSettlementEntered），
        /// 招到的人由 <see cref="OnTroopRecruited"/> 带走各自的那一套装备。
        /// </summary>
        private void OnSettlementEntered(MobileParty? party, Settlement settlement, Hero? hero)
        {
            if (party?.IsLordParty != true || !GwpWardenRecruitment.IsWardenParty(party) ||
                party.MapEvent != null || !IsPreachedVillage(settlement))
                return;
            HandOver(party, settlement);
            EnlistZealots(party, settlement);
            CompleteErrand(party, settlement);
        }

        private void OnTroopRecruited(Hero? recruiter, Settlement? settlement, Hero? source,
            CharacterObject troop, int count)
        {
            MobileParty? party = recruiter?.PartyBelongedTo;
            if (party == null || settlement == null || !GwpWardenRecruitment.IsWardenParty(party) ||
                !IsFireswornPriest(source))
                return;
            MoveKits(settlement.StringId, party.StringId, count, "priest_recruit");
        }

        private void OnMapEventEnded(MapEvent mapEvent)
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_BATTLE_LOSS", () => SettleBattleLosses(mapEvent));
        }

        private void OnMobilePartyDestroyed(MobileParty party, PartyBase? destroyer)
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PARTY_DESTROYED", () => OnPartyGone(party));
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

        // ---------------------------------------------------------------- 祭司

        /// <summary>
        /// 灰袍重建完一座村子时给它拉起一位永恒之火祭司（用户裁定：重建只负责拉起祭司）。
        /// 重建过的村子同时算作信众村。祭司的志愿兵见 <see cref="GwpFireswornVolunteerModel"/>。
        /// </summary>
        internal static void OnVillageRebuilt(Settlement village)
        {
            _instance?.InstallPriest(village);
        }

        private void InstallPriest(Settlement village)
        {
            FaithVillage row = EnsureVillage(village);
            PushMilitia(row, village);
            if (FindLivingPriest(row) != null)
                return;

            // 教义是母系的，祭司只能是女人（用户裁定）。原版每个文化恰有一个女性传教者模板
            // （spc_notable_<文化>_N，is_female），长相在她的捏脸范围里随机。
            CharacterObject template = CharacterObject.All.First(character =>
                character.IsTemplate && character.Occupation == Occupation.Preacher &&
                character.IsFemale && character.Culture == village.Culture);

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
            if (priest.Power < GwpTuning.Firesworn.PriestInitialPower)
                priest.AddPower(GwpTuning.Firesworn.PriestInitialPower - priest.Power);
            AnointPriest(row, priest, village);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_priest_arrived}{PRIEST} has settled in {VILLAGE} to tend the Eternal Fire.",
                "PRIEST", priest.Name, "VILLAGE", village.Name), Colors.Gray));
        }

        /// <summary>
        /// 让一位女性要人成为这座村子的祭司：取祭司名池里的名字、穿袍子、写教义、登记。
        /// 原版模板自带的名字池很小，同文化的传教者经常重名，所以名字由我们给。
        /// </summary>
        private void AnointPriest(FaithVillage row, Hero priest, Settlement village)
        {
            HashSet<string> usedNames = new HashSet<string>(_villages.Values
                .Select(FindLivingPriest)
                .Where(hero => hero != null && hero != priest)
                .Select(hero => hero!.FirstName.ToString()), StringComparer.Ordinal);
            TextObject firstName = new TextObject(GreyWardenFamilyBehavior.PickUniqueName(
                PriestNames, usedNames, MBRandom.RandomInt(PriestNames.Length)));
            priest.SetName(GwpText.Create("{=gwp_fs_priest_name}Eternal Fire Priestess {FIRSTNAME}",
                "FIRSTNAME", firstName), firstName);
            priest.SetTraitLevel(DefaultTraits.Mercy, -1);
            priest.EncyclopediaText = GwpText.Create(
                "{=gwp_fs_priest_lore}{PRIEST} keeps the Eternal Fire burning in {VILLAGE}. The faith she preaches holds that the flame passes only from mother to daughter: women alone may tend it, and men may only feed it, with their labour, with their obedience, and, when they will not kneel, with themselves. Under her the women of the village swear themselves to the fire and take up the spear; its men carry wood to the pyre and keep their eyes lowered.",
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
        /// 补来的是女人就接任祭司；是男人就只是原版传教者，不接火（用户裁定：祭司不能是男的）。
        /// 没补就空着（「墓地」等火蜥蜴再做）。
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
                    hero.IsPreacher && hero.IsFemale && hero.IsAlive && !_priestIds.Contains(hero.StringId));
                if (successor != null)
                    AnointPriest(row, successor, village);
            }
        }

        /// <summary>
        /// 祭司能练出好的灰袍兵（用户裁定）。原版格子里的兵每天只按 log2(power/阶)×1% 升一级，
        /// 而且到 4 阶就停（C#反编译 v1.4.8 RecruitmentCampaignBehavior.UpdateVolunteersOfNotablesInSettlement、
        /// DefaultVolunteerModel.MaxVolunteerTier）。祭司格子里的灰袍兵另外每天按
        /// 基础概率 + power 加成升一级，一直能升到兵线尽头。
        /// </summary>
        private void TrainPriestVolunteers()
        {
            foreach (FaithVillage row in _villages.Values)
            {
                Hero? priest = FindLivingPriest(row);
                if (priest == null)
                    continue;
                float chance = GwpTuning.Firesworn.PriestTrainingDailyChance +
                               priest.Power / GwpTuning.Firesworn.PriestTrainingPowerPerChance;
                for (int i = 0; i < priest.VolunteerTypes.Length; i++)
                {
                    CharacterObject? troop = priest.VolunteerTypes[i];
                    if (troop == null || troop.UpgradeTargets.Length == 0 || MBRandom.RandomFloat >= chance)
                        continue;
                    priest.VolunteerTypes[i] = troop.UpgradeTargets[MBRandom.RandomInt(troop.UpgradeTargets.Length)];
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
        /// 祭司由要人系统按狂信者比例自动生成（用户裁定）。照原版补要人的节奏：每座村每 7 天查一次
        /// （C#反编译 v1.4.8 NotablesCampaignBehavior.DailyTickSettlement → SettlementHelper.SpawnNotablesIfNeeded），
        /// 哪一天查由村子决定。没有祭司、狂信者占户数到了门槛，就按概率来一位。每村只有一位祭司（用户裁定）。
        /// </summary>
        private void SpawnPriestsByShare()
        {
            int dayOfWeek = (int)CampaignTime.Now.ToDays % 7;
            foreach (FaithVillage row in _villages.Values.ToList())
            {
                if (StableDay(row.VillageId, 7) != dayOfWeek || FindLivingPriest(row) != null)
                    continue;
                Settlement village = Settlement.Find(row.VillageId);
                float share = row.Zealots / Math.Max(1f, village.Village.Hearth);
                if (share < GwpTuning.Firesworn.PriestZealotShare ||
                    MBRandom.RandomFloat >= GwpTuning.Firesworn.PriestWeeklyChance)
                    continue;
                InstallPriest(village);
                GwpAiDiagnostics.WriteFaith("PRIEST_SPAWNED",
                    "village=" + village.StringId + "; zealots=" + row.Zealots +
                    "; hearth=" + village.Village.Hearth.ToString("0") + "; share=" + share.ToString("0.00"));
            }
        }

        /// <summary>按村子 id 给一个固定的日子（0…period−1），各村错开，不在同一天扎堆。</summary>
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

        // ---------------------------------------------------------------- 民兵＝狂信者

        private FaithVillage EnsureVillage(Settlement village)
        {
            if (_villages.TryGetValue(village.StringId, out FaithVillage? row))
                return row;

            // 第一次传教时，村里原有的民兵立誓成为狂信者（不超过上限）。
            row = new FaithVillage
            {
                VillageId = village.StringId,
                Zealots = Math.Min(ZealotCap(village), MilitiaHeadcount(village))
            };
            _villages[village.StringId] = row;
            return row;
        }

        private void ObserveAndPushMilitia(FaithVillage row)
        {
            Settlement? village = Settlement.Find(row.VillageId);
            if (village?.IsVillage != true)
                return;
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.MapEvent != null)
                return;

            // 战斗减员、被俘都是真死（佚亡＝原版死亡）：民兵队比记录少就照少的算。
            int observed = MilitiaHeadcount(village);
            if (observed < row.Zealots)
                row.Zealots = observed;
            // 上限是户数；户数掉了（被直接招走、被劫），多出来的狂信者也跟着少。
            row.Zealots = Math.Min(row.Zealots, ZealotCap(village));
            PushMilitia(row, village);
        }

        private static void PushMilitia(FaithVillage row, Settlement village)
        {
            CharacterObject? zealot = CharacterObject.Find(GwpIds.ZealotId);
            if (zealot == null)
                return;
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            if (militia?.MapEvent != null)
                return;

            if (militia?.IsActive != true)
            {
                if (row.Zealots < 2)
                    return;
                // 原版 setter 会建出民兵队并按文化兵种填人，下面再整队换成狂信者。
                village.Militia = row.Zealots;
                militia = village.MilitiaPartyComponent?.MobileParty;
                if (militia?.IsActive != true)
                    return;
            }

            TroopRoster roster = militia.MemberRoster;
            foreach (TroopRosterElement element in roster.GetTroopRoster().ToList())
            {
                if (element.Character == null || element.Character == zealot || element.Number <= 0)
                    continue;
                int number = element.Number;
                int wounded = Math.Min(number, Math.Max(0, element.WoundedNumber));
                roster.AddToCounts(element.Character, -number, false, -wounded);
                roster.AddToCounts(zealot, number, false, wounded);
            }

            int difference = row.Zealots - roster.TotalManCount;
            if (difference > 0)
            {
                roster.AddToCounts(zealot, difference);
            }
            else if (difference < 0)
            {
                TroopRosterElement zealots = roster.GetTroopRoster()
                    .FirstOrDefault(element => element.Character == zealot);
                int remove = Math.Min(-difference, Math.Max(0, zealots.Number));
                int healthy = Math.Max(0, zealots.Number - zealots.WoundedNumber);
                int woundedTaken = Math.Max(0, remove - healthy);
                if (remove > 0)
                    roster.AddToCounts(zealot, -remove, false, -woundedTaken);
            }
        }

        private static int MilitiaHeadcount(Settlement village)
        {
            MobileParty? militia = village.MilitiaPartyComponent?.MobileParty;
            return militia?.IsActive == true ? militia.MemberRoster.TotalManCount : 0;
        }

        // ---------------------------------------------------------------- 工具

        private static bool IsVillageQuiet(Settlement settlement) =>
            settlement.Village?.VillageState == Village.VillageStates.Normal &&
            settlement.Party?.MapEvent == null;

        private static bool HasArrived(MobileParty party, Settlement village) =>
            party.CurrentSettlement == village ||
            party.GetPosition2D.Distance(village.GetPosition2D) <= GwpTuning.Firesworn.ArrivalDistance;

        private static MobileParty? FindParty(string partyId) =>
            string.IsNullOrEmpty(partyId)
                ? null
                : MobileParty.All.FirstOrDefault(party =>
                    string.Equals(party.StringId, partyId, StringComparison.OrdinalIgnoreCase));
    }
}

