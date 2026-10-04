using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// 火誓者第一批底层牌（docs/state/firesworn.md）：狂信者、集会传教、烈火责罚、
    /// 永恒之火祭司与门徒。
    ///
    /// 被传教过的村子在 <see cref="_villages"/> 里有一条记录；没有记录的村子一律走原版。
    /// 有记录的村子民兵完全由狂信者取代：民兵队里只有 <c>gwzealot</c>，人数就是狂信者数，
    /// 原版每日增减由 <see cref="GwpFireswornMilitiaModel"/> 归零，战斗减员从民兵队读回。
    /// </summary>
    public sealed class GwpFireswornBehavior : CampaignBehaviorBase
    {
        private const string VillageIdsKey = "GWPP_FsVillageIds";
        private const string VillageZealotsKey = "GWPP_FsVillageZealots";
        private const string VillageCongregationKey = "GWPP_FsVillageLastCongregation";
        private const string VillageDamnationKey = "GWPP_FsVillageLastDamnation";
        private const string VillageDiscipleKey = "GWPP_FsVillageLastDisciple";
        private const string VillagePriestKey = "GWPP_FsVillagePriest";
        private const string RitualVillageKey = "GWPP_FsRitualVillage";
        private const string RitualKindKey = "GWPP_FsRitualKind";
        private const string RitualHostKey = "GWPP_FsRitualHost";
        private const string RitualStartedKey = "GWPP_FsRitualStarted";
        private const string RitualQueuedKey = "GWPP_FsRitualQueued";
        private const string RitualEndKey = "GWPP_FsRitualEnd";
        private const string LordIdsKey = "GWPP_FsLordIds";
        private const string LordHoursKey = "GWPP_FsLordLastCongregation";
        private const string DiscipleIdsKey = "GWPP_FsDiscipleIds";
        private const string DiscipleHomesKey = "GWPP_FsDiscipleHomes";
        private const double Never = -1000000d;

        private static GwpFireswornBehavior? _instance;

        private readonly Dictionary<string, FaithVillage> _villages =
            new Dictionary<string, FaithVillage>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Ritual> _rituals = new List<Ritual>();
        private readonly Dictionary<string, double> _lordLastCongregation =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _discipleHomes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _priestIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // 只烧不信教者的那一掷每位领主每天一次；读档后重掷无妨，不存档。
        private readonly Dictionary<string, int> _unbelieverRollDay =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        internal enum RitualKind
        {
            Congregation = 0,
            Damnation = 1
        }

        private sealed class FaithVillage
        {
            public string VillageId = string.Empty;
            public int Zealots;
            public double LastCongregation = Never;
            public double LastDamnation = Never;
            public double LastDisciple = Never;
            public string PriestId = string.Empty;
        }

        private sealed class Ritual
        {
            public string VillageId = string.Empty;
            public RitualKind Kind;
            public string HostPartyId = string.Empty;
            public bool Started;
            public double QueuedHour;
            public double EndHour;
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
        }

        public override void SyncData(IDataStore dataStore)
        {
            List<string>? villageIds = null, priests = null;
            List<int>? zealots = null;
            List<double>? congregations = null, damnations = null, disciples = null;
            List<string>? ritualVillages = null, ritualHosts = null;
            List<int>? ritualKinds = null, ritualStarted = null;
            List<double>? ritualQueued = null, ritualEnds = null;
            List<string>? lordIds = null, discipleIds = null, discipleHomes = null;
            List<double>? lordHours = null;

            if (dataStore.IsSaving)
            {
                List<FaithVillage> rows = _villages.Values.ToList();
                villageIds = rows.Select(row => row.VillageId).ToList();
                zealots = rows.Select(row => row.Zealots).ToList();
                congregations = rows.Select(row => row.LastCongregation).ToList();
                damnations = rows.Select(row => row.LastDamnation).ToList();
                disciples = rows.Select(row => row.LastDisciple).ToList();
                priests = rows.Select(row => row.PriestId).ToList();
                ritualVillages = _rituals.Select(ritual => ritual.VillageId).ToList();
                ritualKinds = _rituals.Select(ritual => (int)ritual.Kind).ToList();
                ritualHosts = _rituals.Select(ritual => ritual.HostPartyId).ToList();
                ritualStarted = _rituals.Select(ritual => ritual.Started ? 1 : 0).ToList();
                ritualQueued = _rituals.Select(ritual => ritual.QueuedHour).ToList();
                ritualEnds = _rituals.Select(ritual => ritual.EndHour).ToList();
                lordIds = _lordLastCongregation.Keys.ToList();
                lordHours = lordIds.Select(id => _lordLastCongregation[id]).ToList();
                discipleIds = _discipleHomes.Keys.ToList();
                discipleHomes = discipleIds.Select(id => _discipleHomes[id]).ToList();
            }

            dataStore.SyncData(VillageIdsKey, ref villageIds);
            dataStore.SyncData(VillageZealotsKey, ref zealots);
            dataStore.SyncData(VillageCongregationKey, ref congregations);
            dataStore.SyncData(VillageDamnationKey, ref damnations);
            dataStore.SyncData(VillageDiscipleKey, ref disciples);
            dataStore.SyncData(VillagePriestKey, ref priests);
            dataStore.SyncData(RitualVillageKey, ref ritualVillages);
            dataStore.SyncData(RitualKindKey, ref ritualKinds);
            dataStore.SyncData(RitualHostKey, ref ritualHosts);
            dataStore.SyncData(RitualStartedKey, ref ritualStarted);
            dataStore.SyncData(RitualQueuedKey, ref ritualQueued);
            dataStore.SyncData(RitualEndKey, ref ritualEnds);
            dataStore.SyncData(LordIdsKey, ref lordIds);
            dataStore.SyncData(LordHoursKey, ref lordHours);
            dataStore.SyncData(DiscipleIdsKey, ref discipleIds);
            dataStore.SyncData(DiscipleHomesKey, ref discipleHomes);

            if (!dataStore.IsLoading)
                return;

            _villages.Clear();
            int villageCount = new[]
            {
                villageIds?.Count ?? 0, zealots?.Count ?? 0, congregations?.Count ?? 0,
                damnations?.Count ?? 0, disciples?.Count ?? 0, priests?.Count ?? 0
            }.Min();
            for (int i = 0; i < villageCount; i++)
            {
                if (string.IsNullOrWhiteSpace(villageIds![i]))
                    continue;
                _villages[villageIds[i]] = new FaithVillage
                {
                    VillageId = villageIds[i],
                    Zealots = Math.Max(0, zealots![i]),
                    LastCongregation = congregations![i],
                    LastDamnation = damnations![i],
                    LastDisciple = disciples![i],
                    PriestId = priests![i] ?? string.Empty
                };
            }

            _rituals.Clear();
            int ritualCount = new[]
            {
                ritualVillages?.Count ?? 0, ritualKinds?.Count ?? 0, ritualHosts?.Count ?? 0,
                ritualStarted?.Count ?? 0, ritualQueued?.Count ?? 0, ritualEnds?.Count ?? 0
            }.Min();
            for (int i = 0; i < ritualCount; i++)
            {
                if (string.IsNullOrWhiteSpace(ritualVillages![i]))
                    continue;
                _rituals.Add(new Ritual
                {
                    VillageId = ritualVillages[i],
                    Kind = (RitualKind)ritualKinds![i],
                    HostPartyId = ritualHosts![i] ?? string.Empty,
                    Started = ritualStarted![i] != 0,
                    QueuedHour = ritualQueued![i],
                    EndHour = ritualEnds![i]
                });
            }

            _lordLastCongregation.Clear();
            int lordCount = Math.Min(lordIds?.Count ?? 0, lordHours?.Count ?? 0);
            for (int i = 0; i < lordCount; i++)
                if (!string.IsNullOrWhiteSpace(lordIds![i]))
                    _lordLastCongregation[lordIds[i]] = lordHours![i];

            _discipleHomes.Clear();
            int discipleCount = Math.Min(discipleIds?.Count ?? 0, discipleHomes?.Count ?? 0);
            for (int i = 0; i < discipleCount; i++)
                if (!string.IsNullOrWhiteSpace(discipleIds![i]))
                    _discipleHomes[discipleIds[i]] = discipleHomes![i] ?? string.Empty;

            RebuildPriestIndex();
        }

        // ---------------------------------------------------------------- 查询

        internal static bool IsPreachedVillage(Settlement? settlement) =>
            settlement?.IsVillage == true && _instance != null &&
            _instance._villages.ContainsKey(settlement.StringId);

        internal static bool IsFireswornPriest(Hero? hero) =>
            hero != null && _instance != null && _instance._priestIds.Contains(hero.StringId);

        internal static bool IsDiscipleParty(MobileParty? party) =>
            party?.StringId?.StartsWith(GwpIds.DisciplePartyPrefix, StringComparison.Ordinal) == true;

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
            MobileParty? host = FindParty(ritual.HostPartyId);
            hostName = host?.LeaderHero?.Name?.ToString() ?? host?.Name?.ToString() ?? string.Empty;
            return true;
        }

        internal static int ZealotCap(Settlement settlement)
        {
            float hearth = settlement.Village?.Hearth ?? 0f;
            return Math.Max(GwpTuning.Firesworn.ZealotMinimumCap,
                (int)Math.Round(hearth * GwpTuning.Firesworn.ZealotHearthCap));
        }

        // ---------------------------------------------------------------- 事件

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            GwpFireswornMenus.Register(starter);
            RebuildPriestIndex();
            foreach (FaithVillage row in _villages.Values.ToList())
                ObserveAndPushMilitia(row);
        }

        private void OnHourlyTick()
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_RITUALS", UpdateRituals);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_LORD_CONGREGATION", AssignLordCongregations);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_DAMNATION", TryLordDamnations);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_MILITIA", () =>
            {
                foreach (FaithVillage row in _villages.Values.ToList())
                    ObserveAndPushMilitia(row);
            });
        }

        private void OnDailyTick()
        {
            GwpRuntimeFaultWatch.Guard("FIRESWORN_PRIESTS", MaintainPriests);
            GwpRuntimeFaultWatch.Guard("FIRESWORN_DISCIPLES", DispatchDisciples);
        }

        // ---------------------------------------------------------------- 仪式

        private void UpdateRituals()
        {
            double now = CampaignTime.Now.ToHours;
            foreach (Ritual ritual in _rituals.ToList())
            {
                Settlement? village = Settlement.Find(ritual.VillageId);
                if (village?.IsVillage != true)
                {
                    _rituals.Remove(ritual);
                    continue;
                }

                if (ritual.Kind == RitualKind.Damnation)
                {
                    if (now >= ritual.EndHour)
                        _rituals.Remove(ritual);
                    continue;
                }

                MobileParty? host = FindParty(ritual.HostPartyId);
                bool disciple = IsDiscipleParty(host) || _discipleHomes.ContainsKey(ritual.HostPartyId);
                if (host?.IsActive != true || (!disciple && host.LeaderHero?.IsActive != true))
                {
                    CancelCongregation(ritual, host, "host_lost");
                    continue;
                }

                if (!disciple && IsLordClaimedElsewhere(host))
                {
                    CancelCongregation(ritual, host, "claimed_by_other_duty");
                    continue;
                }

                if (host.MapEvent != null && !host.MapEvent.IsFinalized)
                    continue;

                if (!IsVillageQuiet(village))
                {
                    CancelCongregation(ritual, host, "village_unrest");
                    continue;
                }

                if (!ritual.Started)
                {
                    if (HasArrived(host, village))
                    {
                        ritual.Started = true;
                        ritual.EndHour = now + GwpTuning.Firesworn.CongregationHours;
                        GreyWardenPartyDesireBehavior.RequestVisit(host, village, 10f);
                        InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                            "{=gwp_fs_congregation_started}{HOST} has opened a Congregation of the Eternal Fire in {VILLAGE}.",
                            "HOST", HostName(host), "VILLAGE", village.Name), Colors.Gray));
                        GwpAiDiagnostics.WriteAction(host, "FIRESWORN_CONGREGATION_STARTED",
                            "village=" + village.StringId + "; end=" + ritual.EndHour);
                        continue;
                    }

                    double timeout = disciple
                        ? GwpTuning.Firesworn.DiscipleTravelTimeoutHours
                        : GwpTuning.Firesworn.LordTravelTimeoutHours;
                    if (now - ritual.QueuedHour > timeout)
                    {
                        CancelCongregation(ritual, host, "travel_timeout");
                        continue;
                    }

                    GreyWardenPartyDesireBehavior.RequestVisit(host, village, 8f);
                    continue;
                }

                GreyWardenPartyDesireBehavior.RequestVisit(host, village, 10f);
                if (now >= ritual.EndHour)
                    CompleteCongregation(ritual, host, village, disciple);
            }
        }

        private void CompleteCongregation(Ritual ritual, MobileParty host, Settlement village,
            bool disciple)
        {
            _rituals.Remove(ritual);
            FaithVillage row = EnsureVillage(village);
            int cap = ZealotCap(village);
            float hearth = village.Village?.Hearth ?? 0f;
            int gain = Math.Max(GwpTuning.Firesworn.CongregationMinimumGain,
                (int)Math.Round(hearth * GwpTuning.Firesworn.CongregationHearthShare));
            int carried = disciple ? Math.Max(0, host.MemberRoster.TotalManCount) : 0;
            int before = row.Zealots;
            row.Zealots = Math.Min(cap, row.Zealots + gain + carried);
            row.LastCongregation = CampaignTime.Now.ToHours;
            PushMilitia(row, village);

            if (disciple)
            {
                _discipleHomes.Remove(host.StringId);
                host.MemberRoster.Clear();
                GwpCommon.TryDestroyParty(host);
            }
            else
            {
                if (host.LeaderHero != null)
                    _lordLastCongregation[host.LeaderHero.StringId] = CampaignTime.Now.ToHours;
                GreyWardenPartyDesireBehavior.ClearIntent(host);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(host);
            }

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_congregation_done}The Congregation in {VILLAGE} has ended. The village now counts {ZEALOTS} zealots.",
                "VILLAGE", village.Name, "ZEALOTS", row.Zealots), Colors.Gray));
            GwpAiDiagnostics.WriteAction(host, "FIRESWORN_CONGREGATION_COMPLETED",
                "village=" + village.StringId + "; before=" + before + "; gain=" + gain +
                "; carried=" + carried + "; after=" + row.Zealots + "; cap=" + cap);
        }

        private void CancelCongregation(Ritual ritual, MobileParty? host, string reason)
        {
            _rituals.Remove(ritual);
            Settlement? village = Settlement.Find(ritual.VillageId);
            if (host != null && _discipleHomes.TryGetValue(host.StringId, out string home))
            {
                // 门徒没传成：带出去的人回原村，队伍就地解散。
                _discipleHomes.Remove(host.StringId);
                Settlement? homeVillage = Settlement.Find(home);
                if (host.IsActive && homeVillage != null &&
                    _villages.TryGetValue(homeVillage.StringId, out FaithVillage? homeRow))
                {
                    homeRow.Zealots = Math.Min(ZealotCap(homeVillage),
                        homeRow.Zealots + host.MemberRoster.TotalManCount);
                    PushMilitia(homeRow, homeVillage);
                }
                if (host.IsActive)
                {
                    host.MemberRoster.Clear();
                    GwpCommon.TryDestroyParty(host);
                }
            }
            else if (host?.IsActive == true)
            {
                GreyWardenPartyDesireBehavior.ClearIntent(host);
                GreyWardenPartyDesireBehavior.RequestImmediateRethink(host);
            }

            if (ritual.Started && village != null)
                InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                    "{=gwp_fs_congregation_broken}The Congregation in {VILLAGE} broke up before it was finished.",
                    "VILLAGE", village.Name), Colors.Gray));
            if (host != null)
                GwpAiDiagnostics.WriteAction(host, "FIRESWORN_CONGREGATION_CANCELLED",
                    "village=" + ritual.VillageId + "; started=" + ritual.Started + "; reason=" + reason);
        }

        // ---------------------------------------------------------------- 灰袍领主传教

        private void AssignLordCongregations()
        {
            int active = _rituals.Count(ritual => ritual.Kind == RitualKind.Congregation &&
                !_discipleHomes.ContainsKey(ritual.HostPartyId));
            if (active >= GwpTuning.Firesworn.MaxConcurrentLordCongregations)
                return;

            // 传教是最低一档的活：有待派案件、待重建、待办请愿或本职差事时都不开。
            if (CrimePool.IsDispatchReady ||
                GreyWardenVillageReconstructionBehavior.HasAvailableReconstruction() ||
                GreyWardenIssueResolutionBehavior.HasAvailableIssue())
                return;

            Clan? police = PoliceStats.GetPoliceClan();
            if (police == null)
                return;

            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties()
                         .Where(IsIdleLord)
                         .OrderBy(party => party.StringId, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (active >= GwpTuning.Firesworn.MaxConcurrentLordCongregations)
                    break;

                Settlement? village = Village.All
                    .Select(entry => entry.Settlement)
                    .Where(settlement => IsCongregationTarget(settlement, police))
                    .OrderBy(settlement => lord.GetPosition2D.Distance(settlement.GetPosition2D))
                    .FirstOrDefault();
                if (village == null)
                    return;

                _rituals.Add(new Ritual
                {
                    VillageId = village.StringId,
                    Kind = RitualKind.Congregation,
                    HostPartyId = lord.StringId,
                    QueuedHour = CampaignTime.Now.ToHours
                });
                active++;
                GreyWardenPartyDesireBehavior.RequestVisit(lord, village, 8f);
                GwpAiDiagnostics.WriteAction(lord, "FIRESWORN_CONGREGATION_ASSIGNED",
                    "village=" + village.StringId + "; preached=" + IsPreachedVillage(village));
            }
        }

        private bool IsIdleLord(MobileParty? party)
        {
            if (party?.IsActive != true || !party.IsLordParty || party.IsDisbanding ||
                party.LeaderHero?.IsActive != true || party.LeaderHero.IsPrisoner ||
                party.MapEvent != null || party.IsCurrentlyAtSea || IsHosting(party) ||
                IsLordClaimedElsewhere(party) ||
                GreyWardenDutyScheduler.HasPreferredWork(party))
                return false;

            return !_lordLastCongregation.TryGetValue(party.LeaderHero.StringId, out double last) ||
                   CampaignTime.Now.ToHours - last >=
                   GwpTuning.Firesworn.LordCongregationCooldownDays * 24d;
        }

        private static bool IsLordClaimedElsewhere(MobileParty party) =>
            party.Army != null ||
            CrimePool.HasTask(party.StringId) ||
            PoliceEnforcementBehavior.IsPartyOccupiedByAssistance(party) ||
            GreyWardenVillageAdoptionBehavior.IsVillageReliefParty(party) ||
            GreyWardenVillageReconstructionBehavior.IsReconstructionParty(party) ||
            GreyWardenIssueResolutionBehavior.IsIssueDutyParty(party) ||
            GreyWardenTrainingBehavior.ShouldReserveFromNewDuties(party) ||
            GreyWardenPlayerRequestBehavior.IsPartyReservedForPlayerRequest(party) ||
            GreyWardenTroopRequestBehavior.IsTrainerReservedForPlayerOrder(party);

        private bool IsHosting(MobileParty party) => _rituals.Any(ritual =>
            string.Equals(ritual.HostPartyId, party.StringId, StringComparison.OrdinalIgnoreCase));

        private bool IsCongregationTarget(Settlement? settlement, Clan police)
        {
            if (settlement?.IsVillage != true || !IsVillageQuiet(settlement) ||
                HasRitual(settlement) || FactionManager.IsAtWarAgainstFaction(police, settlement.MapFaction))
                return false;
            if (!_villages.TryGetValue(settlement.StringId, out FaithVillage? row))
                return true;
            return row.Zealots < ZealotCap(settlement) &&
                   CampaignTime.Now.ToHours - row.LastCongregation >=
                   GwpTuning.Firesworn.CongregationVillageIntervalDays * 24d;
        }

        private bool HasRitual(Settlement settlement) => _rituals.Any(ritual =>
            string.Equals(ritual.VillageId, settlement.StringId, StringComparison.OrdinalIgnoreCase));

        // ---------------------------------------------------------------- 烈火责罚

        private void TryLordDamnations()
        {
            int today = (int)Math.Floor(CampaignTime.Now.ToDays);
            foreach (MobileParty lord in PoliceStats.GetAllPoliceParties().ToList())
            {
                Hero? leader = lord.LeaderHero;
                Settlement? village = lord.CurrentSettlement;
                if (!lord.IsActive || leader?.IsActive != true || lord.MapEvent != null ||
                    village?.IsVillage != true || HasRitual(village) || !IsVillageQuiet(village) ||
                    !_villages.TryGetValue(village.StringId, out FaithVillage? row) ||
                    row.Zealots <= 0 || row.Zealots >= ZealotCap(village) ||
                    CampaignTime.Now.ToHours - row.LastDamnation <
                    GwpTuning.Firesworn.DamnationVillageIntervalDays * 24d)
                    continue;

                int prisoners = CountBurnablePrisoners(lord);
                if (prisoners < GwpTuning.Firesworn.DamnationMinimumPrisoners)
                {
                    if (_unbelieverRollDay.TryGetValue(leader.StringId, out int rolled) && rolled == today)
                        continue;
                    _unbelieverRollDay[leader.StringId] = today;
                    if (MBRandom.RandomFloat >= GwpTuning.Firesworn.DamnationUnbelieverOnlyDailyChance)
                        continue;
                    prisoners = 0;
                }

                HoldDamnation(lord, leader, village, row, prisoners);
            }
        }

        private void HoldDamnation(MobileParty lord, Hero leader, Settlement village,
            FaithVillage row, int burnablePrisoners)
        {
            float hearth = village.Village?.Hearth ?? 0f;
            int prisoners = Math.Min(burnablePrisoners, GwpTuning.Firesworn.DamnationMaximumPrisoners);
            int unbelievers = GwpTuning.Firesworn.DamnationUnbelieverBase +
                              (int)(hearth / GwpTuning.Firesworn.DamnationHearthPerUnbeliever);
            int affordable = Math.Max(0, leader.Gold) / GwpTuning.Firesworn.DamnationCostPerHead;
            unbelievers = Math.Max(0, Math.Min(unbelievers, affordable - prisoners));
            prisoners = Math.Min(prisoners, affordable);
            int burned = prisoners + unbelievers;
            if (burned <= 0)
                return;

            RemoveBurnablePrisoners(lord, prisoners);
            int cost = burned * GwpTuning.Firesworn.DamnationCostPerHead;
            GiveGoldAction.ApplyBetweenCharacters(leader, null, cost, true);

            int before = row.Zealots;
            row.Zealots = Math.Min(ZealotCap(village), row.Zealots + burned);
            row.LastDamnation = CampaignTime.Now.ToHours;
            PushMilitia(row, village);
            _rituals.Add(new Ritual
            {
                VillageId = village.StringId,
                Kind = RitualKind.Damnation,
                HostPartyId = lord.StringId,
                Started = true,
                QueuedHour = CampaignTime.Now.ToHours,
                EndHour = CampaignTime.Now.ToHours + GwpTuning.Firesworn.DamnationLockHours
            });

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_damnation_done}{HOST} held a Damnation in {VILLAGE}: {BURNED} burned, and the village now counts {ZEALOTS} zealots.",
                "HOST", leader.Name, "VILLAGE", village.Name, "BURNED", burned,
                "ZEALOTS", row.Zealots), Colors.Gray));
            GwpAiDiagnostics.WriteAction(lord, "FIRESWORN_DAMNATION",
                "village=" + village.StringId + "; prisoners=" + prisoners +
                "; unbelievers=" + unbelievers + "; cost=" + cost +
                "; before=" + before + "; after=" + row.Zealots);
        }

        private static int CountBurnablePrisoners(MobileParty party) =>
            party.PrisonRoster.GetTroopRoster()
                .Where(element => element.Character != null && !element.Character.IsHero)
                .Sum(element => Math.Max(0, element.Number));

        private static void RemoveBurnablePrisoners(MobileParty party, int count)
        {
            foreach (TroopRosterElement element in party.PrisonRoster.GetTroopRoster()
                         .Where(element => element.Character != null && !element.Character.IsHero)
                         .ToList())
            {
                if (count <= 0)
                    return;
                int take = Math.Min(count, Math.Max(0, element.Number));
                int wounded = Math.Min(take, Math.Max(0, element.WoundedNumber));
                party.PrisonRoster.AddToCounts(element.Character, -take, false, -wounded);
                count -= take;
            }
        }

        // ---------------------------------------------------------------- 门徒

        private void DispatchDisciples()
        {
            foreach (string partyId in _discipleHomes.Keys.ToList())
                if (FindParty(partyId)?.IsActive != true)
                    _discipleHomes.Remove(partyId);

            Clan? police = PoliceStats.GetPoliceClan();
            if (police?.Leader == null)
                return;

            foreach (FaithVillage row in _villages.Values
                         .OrderByDescending(row => row.Zealots)
                         .ThenBy(row => row.VillageId, StringComparer.OrdinalIgnoreCase)
                         .ToList())
            {
                if (_discipleHomes.Count >= GwpTuning.Firesworn.MaxActiveDisciples)
                    return;

                Settlement? home = Settlement.Find(row.VillageId);
                if (home?.IsVillage != true || !IsVillageQuiet(home) || HasRitual(home) ||
                    CampaignTime.Now.ToHours - row.LastDisciple <
                    GwpTuning.Firesworn.DiscipleIntervalDays * 24d ||
                    row.Zealots < GwpTuning.Firesworn.DiscipleSize +
                    GwpTuning.Firesworn.DiscipleMinimumLeftBehind)
                    continue;

                Hero? priest = FindLivingPriest(row);
                if (row.Zealots < GwpTuning.Firesworn.DiscipleZealotThreshold &&
                    (priest == null || priest.Power < GwpTuning.Firesworn.DisciplePriestPowerThreshold))
                    continue;

                // 门徒先去还没传过教的村子，其次是还能再传的。
                Settlement? target = Village.All
                    .Select(entry => entry.Settlement)
                    .Where(settlement => settlement != home &&
                        IsCongregationTarget(settlement, police) &&
                        home.GetPosition2D.Distance(settlement.GetPosition2D) <=
                        GwpTuning.Firesworn.DiscipleTargetMaxDistance)
                    .OrderBy(settlement => IsPreachedVillage(settlement) ? 1 : 0)
                    .ThenBy(settlement => home.GetPosition2D.Distance(settlement.GetPosition2D))
                    .FirstOrDefault();
                if (target == null)
                    continue;

                SpawnDisciple(police, row, home, target);
            }
        }

        private void SpawnDisciple(Clan police, FaithVillage row, Settlement home, Settlement target)
        {
            CharacterObject? zealot = CharacterObject.Find(GwpIds.ZealotId);
            if (zealot == null)
                return;

            MobileParty party = CustomPartyComponent.CreateCustomPartyWithPartyTemplate(
                home.GatePosition, 1f, home,
                GwpText.Create("{=gwp_fs_disciple_party}Disciples of the Eternal Fire"),
                police, police.DefaultPartyTemplate, police.Leader, "", "", 4f, false);
            party.StringId = GwpIds.DisciplePartyPrefix + MBRandom.RandomInt(100000, 999999);
            party.ActualClan = police;
            party.MemberRoster.Clear();
            party.MemberRoster.AddToCounts(zealot, GwpTuning.Firesworn.DiscipleSize);
            PoliceResourceManager.OutfitTemporaryDutyParty(party);

            row.Zealots -= GwpTuning.Firesworn.DiscipleSize;
            row.LastDisciple = CampaignTime.Now.ToHours;
            PushMilitia(row, home);
            _discipleHomes[party.StringId] = home.StringId;
            _rituals.Add(new Ritual
            {
                VillageId = target.StringId,
                Kind = RitualKind.Congregation,
                HostPartyId = party.StringId,
                QueuedHour = CampaignTime.Now.ToHours
            });
            GreyWardenPartyDesireBehavior.RequestVisit(party, target, 8f);

            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_disciple_left}Disciples of the Eternal Fire have left {HOME} to preach in {TARGET}.",
                "HOME", home.Name, "TARGET", target.Name), Colors.Gray));
            GwpAiDiagnostics.WriteAction(party, "FIRESWORN_DISCIPLE_DISPATCHED",
                "home=" + home.StringId + "; target=" + target.StringId +
                "; homeZealots=" + row.Zealots);
        }

        // ---------------------------------------------------------------- 祭司

        /// <summary>
        /// 灰袍重建完一座村子时给它添一位永恒之火祭司（用户裁定）。祭司是原版
        /// <c>Preacher</c> 职业的要人，志愿兵格子只出狂信者（见 <see cref="GwpFireswornVolunteerModel"/>）。
        /// 重建过的村子同时算作已传教。
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

            List<CharacterObject> templates = village.Culture?.NotableTemplates
                .Where(character => character.Occupation == Occupation.Headman ||
                                    character.Occupation == Occupation.RuralNotable)
                .ToList() ?? new List<CharacterObject>();
            if (templates.Count == 0)
                return;
            CharacterObject template = templates[MBRandom.RandomInt(templates.Count)];

            Hero priest = HeroCreator.CreateSpecialHero(template, village, null, null,
                MBRandom.RandomInt(35, 56));
            priest.SetNewOccupation(Occupation.Preacher);
            priest.SetName(GwpText.Create("{=gwp_fs_priest_name}Eternal Fire Priest {FIRSTNAME}",
                "FIRSTNAME", priest.FirstName), priest.FirstName);
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
            DressPriest(priest);

            row.PriestId = priest.StringId;
            _priestIds.Add(priest.StringId);
            InformationManager.DisplayMessage(new InformationMessage(GwpText.Get(
                "{=gwp_fs_priest_arrived}{PRIEST} has settled in {VILLAGE} to tend the Eternal Fire.",
                "PRIEST", priest.Name, "VILLAGE", village.Name), Colors.Gray));
        }

        private static void DressPriest(Hero priest)
        {
            ItemObject? robe = MBObjectManager.Instance.GetObject<ItemObject>("monk_robe");
            ItemObject? hood = MBObjectManager.Instance.GetObject<ItemObject>("pilgrim_hood");
            foreach (Equipment? equipment in new[] { priest.CivilianEquipment, priest.BattleEquipment })
            {
                if (equipment == null)
                    continue;
                if (robe != null)
                    equipment[EquipmentIndex.Body] = new EquipmentElement(robe);
                if (hood != null)
                    equipment[EquipmentIndex.Head] = new EquipmentElement(hood);
            }
        }

        /// <summary>
        /// 祭司死了而原版按高 power 要人的规矩补了一位同职业的亲属时，接着当祭司；
        /// 没补就空着（「墓地」等火蜥蜴再做）。
        /// </summary>
        private void MaintainPriests()
        {
            foreach (FaithVillage row in _villages.Values)
            {
                if (string.IsNullOrEmpty(row.PriestId) || FindLivingPriest(row) != null)
                    continue;
                Settlement? village = Settlement.Find(row.VillageId);
                Hero? successor = village?.Notables.FirstOrDefault(hero =>
                    hero.IsPreacher && hero.IsAlive && !_priestIds.Contains(hero.StringId));
                _priestIds.Remove(row.PriestId);
                row.PriestId = successor?.StringId ?? string.Empty;
                if (successor != null)
                    _priestIds.Add(successor.StringId);
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

        private static string HostName(MobileParty host) =>
            host.LeaderHero?.Name?.ToString() ?? host.Name?.ToString() ?? string.Empty;

        private static MobileParty? FindParty(string partyId) =>
            string.IsNullOrEmpty(partyId)
                ? null
                : MobileParty.All.FirstOrDefault(party =>
                    string.Equals(party.StringId, partyId, StringComparison.OrdinalIgnoreCase));
    }
}
