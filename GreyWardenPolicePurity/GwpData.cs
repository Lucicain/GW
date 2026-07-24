using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    // ===================== 警察执法数据 =====================

    /// <summary>只保存一件当前尚未结案的案件；结案后整条记录会从案件池删除。</summary>
    public class CrimeRecord
    {
        private MobileParty? _offender;
        private Hero? _offenderHero;

        public string CrimeId { get; set; } = string.Empty;
        public string CrimeType { get; set; } = string.Empty;
        public GwpCrimeCategory CrimeCategory { get; set; }
        public string OffenderHeroId { get; set; } = string.Empty;
        public string OffenderPartyId { get; set; } = string.Empty;
        public CampaignTime OccurredTime { get; set; }
        public CampaignTime LastCrimeTime { get; set; }
        public Vec2 Location { get; set; }
        public string VictimName { get; set; } = string.Empty;
        public bool HasOpenCase { get; set; }

        public Hero? OffenderHero
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OffenderHeroId)) return null;
                if (_offenderHero != null &&
                    string.Equals(_offenderHero.StringId, OffenderHeroId, StringComparison.OrdinalIgnoreCase))
                    return _offenderHero;

                try
                {
                    _offenderHero = Hero.FindFirst(h =>
                        string.Equals(h.StringId, OffenderHeroId, StringComparison.OrdinalIgnoreCase));
                }
                catch (ArgumentNullException)
                {
                    // CampaignBehavior.SyncData runs before Bannerlord has finished
                    // constructing Hero's global source collection. Resolution is lazy,
                    // so simply retry after the campaign session has launched.
                    return null;
                }
                return _offenderHero;
            }
        }

        public MobileParty? Offender
        {
            get
            {
                if (CrimeId == CrimePool.PlayerCrimeId)
                    return MobileParty.MainParty;

                Hero? hero = OffenderHero;
                if (hero?.PartyBelongedTo != null)
                {
                    _offender = hero.PartyBelongedTo;
                    OffenderPartyId = _offender.StringId ?? OffenderPartyId;
                    return _offender;
                }

                if (_offender?.IsActive == true)
                    return _offender;

                if (!string.IsNullOrWhiteSpace(OffenderPartyId))
                    _offender = MobileParty.All.FirstOrDefault(p =>
                        string.Equals(p.StringId, OffenderPartyId, StringComparison.OrdinalIgnoreCase));

                return _offender;
            }
            set
            {
                _offender = value;
                if (value == null) return;
                OffenderPartyId = value.StringId ?? OffenderPartyId;
                if (value.LeaderHero != null)
                {
                    _offenderHero = value.LeaderHero;
                    OffenderHeroId = value.LeaderHero.StringId ?? OffenderHeroId;
                }
            }
        }

        public bool IsOffenderValid() => Offender?.IsActive == true;
        public bool IsOffenderPursuable() =>
            HasOpenCase && Offender?.IsActive == true && Offender.CurrentSettlement == null;
    }

    /// <summary>
    /// 英雄页使用的长期数字档案。这里只保留累计次数和威慑数值，绝不保存案件时间、地点、
    /// 受害者或罪名等已结案细节。
    /// </summary>
    public class HeroCrimeStats
    {
        private Hero? _hero;

        public string HeroId { get; set; } = string.Empty;
        public int TotalCrimeCount { get; set; }
        public int TotalArrestCount { get; set; }
        public float DirectDeterrencePoints { get; set; }
        public float SharedDeterrencePoints { get; set; }
        public int SharedDeterrenceCount { get; set; }
        public float LastDeterrenceUpdatedHours { get; set; }
        public float LastEnforcementHours { get; set; }
        public int CaravanArrestCount { get; set; }
        public float CaravanDirectDeterrencePoints { get; set; }
        public float CaravanSharedDeterrencePoints { get; set; }
        public int CaravanSharedDeterrenceCount { get; set; }
        public float CaravanLastDeterrenceUpdatedHours { get; set; }
        public float CaravanLastEnforcementHours { get; set; }

        public Hero? Hero
        {
            get
            {
                if (string.IsNullOrWhiteSpace(HeroId)) return null;
                if (_hero != null &&
                    string.Equals(_hero.StringId, HeroId, StringComparison.OrdinalIgnoreCase))
                    return _hero;

                try
                {
                    _hero = TaleWorlds.CampaignSystem.Hero.FindFirst(hero =>
                        string.Equals(hero.StringId, HeroId, StringComparison.OrdinalIgnoreCase));
                }
                catch (ArgumentNullException)
                {
                    return null;
                }

                return _hero;
            }
        }
    }

    /// <summary>只保存警务流程状态；目标案情通过账本键实时解析，不再复制整条犯罪记录。</summary>
    public class PoliceTask
    {
        private CrimeRecord? _targetCrime;

        public string PolicePartyId { get; set; } = string.Empty;
        public string TargetCrimeId { get; set; } = string.Empty;
        public CrimeRecord? TargetCrime
        {
            get
            {
                if (_targetCrime != null &&
                    string.Equals(_targetCrime.CrimeId, TargetCrimeId, StringComparison.OrdinalIgnoreCase))
                    return _targetCrime;

                _targetCrime = CrimePool.GetRecordByKey(TargetCrimeId);
                return _targetCrime;
            }
            set
            {
                _targetCrime = value;
                TargetCrimeId = value?.CrimeId ?? string.Empty;
            }
        }

        public bool WarDeclared { get; set; }
        public IFaction? WarTarget { get; set; }
        public bool IsEscortingPlayer { get; set; }
        public bool IsPreparingDispatch { get; set; }
        public Settlement? EscortSettlement { get; set; }
        public bool IsPlayerBountyEscort { get; set; }
        public float LeaderSoloSpeedAtAssignment { get; set; }
        public bool HasTheoreticalLeaderSoloSpeedAtAssignment { get; set; }

        public bool IsTargetValid() => TargetCrime?.IsOffenderValid() == true;

        internal PoliceTaskFlowState FlowState
        {
            get
            {
                if (IsPlayerBountyEscort) return PoliceTaskFlowState.PlayerBountyEscort;
                if (IsEscortingPlayer) return PoliceTaskFlowState.EscortingPlayer;
                if (IsPreparingDispatch) return PoliceTaskFlowState.PreparingDispatch;
                if (WarDeclared) return PoliceTaskFlowState.WarPursuit;
                if (TargetCrime != null) return PoliceTaskFlowState.Pursuit;
                return PoliceTaskFlowState.None;
            }
        }
    }

    public static class PoliceStats
    {
        public const string PoliceClanId = GwpIds.PoliceClanId;

        public static Clan GetPoliceClan() =>
            Clan.FindFirst(c => string.Equals(c.StringId, PoliceClanId, StringComparison.OrdinalIgnoreCase));

        public static List<MobileParty> GetAllPoliceParties()
        {
            Clan clan = GetPoliceClan();
            if (clan == null) return new List<MobileParty>();
            return clan.WarPartyComponents
                .Where(w => w?.MobileParty != null && w.MobileParty.IsActive &&
                            !GwpCommon.IsEnforcementDelayPatrolParty(w.MobileParty))
                .Select(w => w.MobileParty)
                .ToList();
        }

        public static int PartyCount => GetAllPoliceParties().Count;
        public static int MaxActiveTasks => PartyCount;
        public static int PoliceClanMemberCount =>
            GetPoliceClan()?.Heroes.Count(h => h != null && h.IsAlive) ?? 0;

        public static bool CanHandleOrdinaryCase(MobileParty? party) =>
            party?.IsActive == true && party.IsLordParty &&
            party.LeaderHero?.IsActive == true &&
            party.ActualClan == GetPoliceClan();
    }

    /// <summary>
    /// 当前案件池与长期数字档案。案件池只保留未结案件；长期档案只按英雄保存累计数字。
    /// </summary>
    public static class CrimePool
    {
        private static readonly Dictionary<string, CrimeRecord> _ledger =
            new Dictionary<string, CrimeRecord>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, HeroCrimeStats> _history =
            new Dictionary<string, HeroCrimeStats>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PoliceTask> _tasks =
            new Dictionary<string, PoliceTask>(StringComparer.OrdinalIgnoreCase);

        public const string PlayerCrimeId = "PLAYER_WANTED";
        public const int MaxTaskPoolEntries = 100;

        public static bool IsAccepting => true;
        public static bool IsDispatchReady => GetUnassignedOpenCases().Any(c => c.IsOffenderPursuable());
        public static IReadOnlyDictionary<string, PoliceTask> ActiveTasks => _tasks;
        public static IEnumerable<CrimeRecord> LedgerRecords => _ledger.Values;
        public static IEnumerable<HeroCrimeStats> HistoryRecords => _history.Values;

        public static void ClearAll()
        {
            _ledger.Clear();
            _history.Clear();
            _tasks.Clear();
        }

        public static CrimeRecord? GetRecordByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            _ledger.TryGetValue(key, out CrimeRecord? record);
            return record;
        }

        public static CrimeRecord? GetRecord(Hero? hero)
        {
            if (hero == null || string.IsNullOrWhiteSpace(hero.StringId)) return null;
            return GetRecordByKey(hero.StringId);
        }

        public static HeroCrimeStats? GetHistory(Hero? hero)
        {
            if (hero == null || string.IsNullOrWhiteSpace(hero.StringId)) return null;
            _history.TryGetValue(hero.StringId, out HeroCrimeStats? history);
            return history;
        }

        public static HeroCrimeStats GetOrCreateHistory(Hero hero)
        {
            string key = hero.StringId;
            if (_history.TryGetValue(key, out HeroCrimeStats? existing))
                return existing;

            var history = new HeroCrimeStats
            {
                HeroId = key,
                LastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours
            };
            _history[key] = history;
            return history;
        }

        private static CrimeRecord GetOrCreateRecord(Hero hero)
        {
            string key = hero.StringId;
            if (_ledger.TryGetValue(key, out CrimeRecord? existing))
                return existing;

            var record = new CrimeRecord
            {
                CrimeId = key,
                OffenderHeroId = key,
                Offender = hero.PartyBelongedTo,
                OccurredTime = CampaignTime.Now,
                LastCrimeTime = CampaignTime.Now
            };
            _ledger[key] = record;
            return record;
        }

        public static bool IsPlayerHunted =>
            _ledger.TryGetValue(PlayerCrimeId, out CrimeRecord? playerCrime) &&
            playerCrime.HasOpenCase &&
            (playerCrime.IsOffenderValid() || _tasks.Values.Any(t => t.TargetCrimeId == PlayerCrimeId));

        public static bool TryAddPlayerCrime(string crimeType, Vec2 location, string detail)
        {
            MobileParty playerParty = MobileParty.MainParty;
            if (playerParty == null || !playerParty.IsActive || IsPlayerHunted ||
                !IsCategoryIntakeEnabled(GwpCrimeCategory.PlayerCase)) return false;

            _ledger[PlayerCrimeId] = new CrimeRecord
            {
                CrimeId = PlayerCrimeId,
                CrimeType = crimeType,
                CrimeCategory = GwpCrimeCategory.PlayerCase,
                Offender = playerParty,
                OccurredTime = CampaignTime.Now,
                LastCrimeTime = CampaignTime.Now,
                Location = location,
                VictimName = detail,
                HasOpenCase = true
            };
            TrimOpenCasesToCapacity(MaxTaskPoolEntries);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_gwpdata_001}You have been put on the wanted list by the Grey Wardens!"), Colors.Red));
            return true;
        }

        public static void EndPlayerHunt()
        {
            _ledger.Remove(PlayerCrimeId);
            foreach (string key in _tasks.Where(kv => kv.Value.TargetCrimeId == PlayerCrimeId)
                         .Select(kv => kv.Key).ToList())
                _tasks.Remove(key);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_gwpdata_002}The wanted order has been lifted, and the Grey Wardens are no longer hunting you."), Colors.Green));
        }

        public static CrimeRecord? GetPlayerCrime() => GetRecordByKey(PlayerCrimeId);

        public static string? GetPlayerTaskPolicePartyId() =>
            _tasks.FirstOrDefault(kv => kv.Value.TargetCrimeId == PlayerCrimeId).Key;

        public static bool TryAdd(string crimeType, MobileParty offender, Vec2 location, string victimName)
        {
            if (offender == null || !offender.IsActive || GwpCommon.ShouldIgnoreCrimeTracking(offender))
                return false;

            Hero? leader = offender.LeaderHero;
            if (leader == null || string.IsNullOrWhiteSpace(leader.StringId) || leader == Hero.MainHero)
                return false;

            string normalizedCrimeType = crimeType ?? string.Empty;
            GwpCrimeCategory category = GwpCrimeCategoryClassifier.FromCrimeType(
                normalizedCrimeType, leader.StringId);
            // Check the surviving office before touching an existing open record. Otherwise
            // a crime from an extinct duty could overwrite the type/category of a valid case
            // already being pursued against the same offender.
            if (!IsCategoryIntakeEnabled(category))
                return false;

            CrimeRecord record = GetOrCreateRecord(leader);
            CampaignTime now = CampaignTime.Now;
            if (!record.HasOpenCase)
                record.OccurredTime = now;

            record.CrimeType = normalizedCrimeType;
            record.CrimeCategory = category;
            record.Offender = offender;
            record.LastCrimeTime = now;
            record.Location = location;
            record.VictimName = victimName ?? string.Empty;
            record.HasOpenCase = true;
            GetOrCreateHistory(leader).TotalCrimeCount++;
            TrimOpenCasesToCapacity(MaxTaskPoolEntries);
            return true;
        }

        /// <summary>行政改派时重开原案，不增加永久犯罪次数；战败不调用此入口。</summary>
        public static void ReopenCase(CrimeRecord? crime)
        {
            if (crime == null || crime.CrimeId == PlayerCrimeId) return;
            crime.HasOpenCase = true;
            _ledger[crime.CrimeId] = crime;
            TrimOpenCasesToCapacity(MaxTaskPoolEntries);
        }

        public static void TrimOpenCasesToCapacity(int capacity)
        {
            int safeCapacity = Math.Max(0, capacity);
            var assignedCrimeIds = new HashSet<string>(_tasks.Values
                .Select(task => task.TargetCrimeId)
                .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);
            List<CrimeRecord> openCases = _ledger.Values.Where(record => record.HasOpenCase).ToList();
            int removeCount = openCases.Count - safeCapacity;
            if (removeCount <= 0) return;

            foreach (CrimeRecord record in openCases
                         .Where(record => record.CrimeId != PlayerCrimeId &&
                                          !assignedCrimeIds.Contains(record.CrimeId))
                         .OrderBy(record => record.LastCrimeTime.ToHours)
                         .ThenBy(record => record.CrimeId, StringComparer.OrdinalIgnoreCase)
                         .Take(removeCount).ToList())
                _ledger.Remove(record.CrimeId);
        }

        public static int RecordArrest(Hero? hero)
        {
            if (hero == null || hero == Hero.MainHero || string.IsNullOrWhiteSpace(hero.StringId))
                return 0;

            HeroCrimeStats history = GetOrCreateHistory(hero);
            history.TotalArrestCount++;
            return history.TotalArrestCount;
        }

        public static CrimeRecord? GetNearest(Vec2 pos) => SelectNearest(
            GetUnassignedOpenCases().Where(c => c.IsOffenderPursuable()), pos);

        public static CrimeRecord? GetNearest(Vec2 pos, Func<CrimeRecord, bool> predicate) =>
            SelectNearest(GetUnassignedOpenCases().Where(c => c.IsOffenderPursuable() && predicate(c)), pos);

        public static bool IsCategoryIntakeEnabled(GwpCrimeCategory category) => category switch
        {
            GwpCrimeCategory.CaravanAttack => GreyWardenFamilyBehavior.HasLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.CaravanProtection),
            GwpCrimeCategory.VillageViolence => GreyWardenFamilyBehavior.HasLivingDutyHolder(
                GreyWardenFamilyBehavior.DutyKind.VillageProtection),
            // The sixth office is reserved for future player petitions. Existing
            // player wanted cases remain part of the pre-existing criminal system.
            GwpCrimeCategory.PlayerCase => true,
            _ => true
        };

        public static CrimeRecord? GetNearestNonPlayer(Vec2 pos) => SelectNearest(
            GetUnassignedOpenCases().Where(c => c.CrimeId != PlayerCrimeId && c.IsOffenderPursuable()), pos);

        public static CrimeRecord? GetNearestNonPlayerFromAll(Vec2 pos) => SelectNearest(
            _ledger.Values.Where(c => c.CrimeId != PlayerCrimeId && c.HasOpenCase && c.IsOffenderPursuable()), pos);


        private static CrimeRecord? SelectNearest(IEnumerable<CrimeRecord> source, Vec2 pos)
        {
            return source
                .OrderBy(c => c.Offender == null ? float.MaxValue : pos.Distance(c.Offender.GetPosition2D))
                .FirstOrDefault();
        }

        private static IEnumerable<CrimeRecord> GetUnassignedOpenCases()
        {
            var assigned = new HashSet<string>(
                _tasks.Values.Select(t => t.TargetCrimeId).Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.OrdinalIgnoreCase);
            return _ledger.Values.Where(c => c.HasOpenCase && !assigned.Contains(c.CrimeId));
        }

        public static string? GetAssignedPolicePartyId(string? offenderStringId)
        {
            if (string.IsNullOrWhiteSpace(offenderStringId)) return null;
            foreach (var kv in _tasks)
            {
                CrimeRecord? crime = kv.Value.TargetCrime;
                if (string.Equals(crime?.Offender?.StringId, offenderStringId, StringComparison.OrdinalIgnoreCase))
                    return kv.Key;
            }
            return null;
        }

        public static void SetBountyEscortFlag(string policePartyId, bool value)
        {
            if (!string.IsNullOrWhiteSpace(policePartyId) && _tasks.TryGetValue(policePartyId, out PoliceTask? task))
                task.IsPlayerBountyEscort = value;
        }

        public static CrimeRecord? GetByOffenderId(string? partyStringId)
        {
            if (string.IsNullOrWhiteSpace(partyStringId)) return null;
            return _ledger.Values.FirstOrDefault(c =>
                c.HasOpenCase &&
                string.Equals(c.Offender?.StringId, partyStringId, StringComparison.OrdinalIgnoreCase));
        }

        public static bool RemovePendingCrimeByOffenderId(string? offenderStringId)
        {
            if (string.IsNullOrWhiteSpace(offenderStringId)) return false;
            CrimeRecord? record = GetByOffenderId(offenderStringId);
            if (record == null || _tasks.Values.Any(t => t.TargetCrimeId == record.CrimeId))
                return false;
            return _ledger.Remove(record.CrimeId);
        }

        public static void BeginTask(string policePartyId, CrimeRecord crime)
        {
            if (string.IsNullOrWhiteSpace(policePartyId) || crime == null) return;

            // 双向唯一性兜底：一个案件只能有一名承办者，一名警察只能有一案。
            if (_tasks.Any(kv =>
                    !string.Equals(kv.Key, policePartyId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(kv.Value.TargetCrimeId, crime.CrimeId,
                        StringComparison.OrdinalIgnoreCase)))
                return;

            if (_tasks.TryGetValue(policePartyId, out PoliceTask? previous))
            {
                if (string.Equals(previous.TargetCrimeId, crime.CrimeId,
                        StringComparison.OrdinalIgnoreCase))
                    return;

                ReopenCase(previous.TargetCrime);
            }

            _tasks[policePartyId] = new PoliceTask
            {
                PolicePartyId = policePartyId,
                TargetCrime = crime
            };
        }

        /// <summary>
        /// 结束任务即从当前案件池删除案件。战败或承办人失效也直接结案；只有玩家
        /// 案件挤占、村庄救济调度等明确的行政移交路径才会先调用 ReopenCase。
        /// </summary>
        public static void EndTask(string policePartyId)
        {
            if (_tasks.TryGetValue(policePartyId, out PoliceTask? task))
            {
                CrimeRecord? crime = task.TargetCrime;
                _tasks.Remove(policePartyId);
                if (crime != null && crime.CrimeId != PlayerCrimeId)
                {
                    crime.HasOpenCase = false;
                    _ledger.Remove(crime.CrimeId);
                }
            }
        }

        public static PoliceTask? GetTask(string policePartyId)
        {
            _tasks.TryGetValue(policePartyId, out PoliceTask? task);
            return task;
        }

        public static bool HasTask(string policePartyId) => _tasks.ContainsKey(policePartyId);

        public static bool TryAssignPlayerCrimeToPolice(string policePartyId)
        {
            if (string.IsNullOrWhiteSpace(policePartyId)) return false;
            CrimeRecord? playerCrime = GetPlayerCrime();
            if (playerCrime?.HasOpenCase != true) return false;

            if (_tasks.TryGetValue(policePartyId, out PoliceTask? same) &&
                same.TargetCrimeId == PlayerCrimeId)
                return true;

            string? oldPlayerTask = GetPlayerTaskPolicePartyId();
            if (!string.IsNullOrWhiteSpace(oldPlayerTask))
                _tasks.Remove(oldPlayerTask);

            if (_tasks.TryGetValue(policePartyId, out PoliceTask? displaced))
            {
                ReopenCase(displaced.TargetCrime);
                _tasks.Remove(policePartyId);
            }

            _tasks[policePartyId] = new PoliceTask
            {
                PolicePartyId = policePartyId,
                TargetCrimeId = PlayerCrimeId
            };
            return true;
        }

        public static void Clean()
        {
            foreach (string policeId in _tasks.Where(kv =>
                         MobileParty.All.All(p => !string.Equals(p.StringId, kv.Key, StringComparison.OrdinalIgnoreCase)) ||
                         kv.Value.TargetCrime == null)
                     .Select(kv => kv.Key).ToList())
            {
                // 承办部队已经消失或案卷目标无法恢复时视为执法失败。EndTask
                // 会删除普通领主案件；玩家长期通缉记录则按其专门规则保留。
                EndTask(policeId);
            }

            foreach (string crimeId in _ledger.Values.Where(record =>
            {
                Hero? hero = record.OffenderHero;
                return record.CrimeId != PlayerCrimeId && hero != null && !hero.IsAlive;
            }).Select(record => record.CrimeId).ToList())
                _ledger.Remove(crimeId);
        }

        public static List<MobileParty> GetTrackedOffendersByFaction(IFaction? faction)
        {
            if (faction == null) return new List<MobileParty>();
            return GetAllTrackedOffenders()
                .Where(p => p.MapFaction == faction)
                .ToList();
        }

        public static bool HasTrackedOffenderByFaction(IFaction? faction) =>
            GetTrackedOffendersByFaction(faction).Count > 0;

        public static List<MobileParty> GetAllTrackedOffenders(bool includePlayer = false)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<MobileParty>();
            foreach (CrimeRecord record in _ledger.Values)
            {
                if (!record.HasOpenCase) continue;
                MobileParty? party = record.Offender;
                if (party == null || !party.IsActive || (!includePlayer && party.IsMainParty)) continue;
                if (seen.Add(party.StringId)) result.Add(party);
            }
            return result;
        }

        public static void RefreshAccepting() { }

        public static void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                TrimOpenCasesToCapacity(MaxTaskPoolEntries);
                List<CrimeRecord> records = _ledger.Values.Where(record => record.HasOpenCase).ToList();
                int count = records.Count;
                dataStore.SyncData("gwp_ledger_count", ref count);
                for (int i = 0; i < records.Count; i++)
                {
                    HeroCrimeStats history = string.IsNullOrWhiteSpace(records[i].OffenderHeroId)
                        ? new HeroCrimeStats()
                        : _history.TryGetValue(records[i].OffenderHeroId, out HeroCrimeStats? savedHistory)
                            ? savedHistory
                            : new HeroCrimeStats { HeroId = records[i].OffenderHeroId };
                    SyncRecord(dataStore, i, records[i], history, saving: true);
                }

                List<HeroCrimeStats> histories = _history.Values.Where(ShouldPersistHistory).ToList();
                int historyCount = histories.Count;
                dataStore.SyncData("gwp_history_count", ref historyCount);
                for (int i = 0; i < histories.Count; i++)
                    SyncHistory(dataStore, i, histories[i], saving: true);

                List<PoliceTask> tasks = _tasks.Values.Where(t => t.TargetCrime != null).ToList();
                int taskCount = tasks.Count;
                dataStore.SyncData("gwp_ledger_task_count", ref taskCount);
                for (int i = 0; i < tasks.Count; i++)
                    SyncTask(dataStore, i, tasks[i], saving: true);
            }
            else if (dataStore.IsLoading)
            {
                _ledger.Clear();
                _history.Clear();
                _tasks.Clear();

                int count = 0;
                dataStore.SyncData("gwp_ledger_count", ref count);
                for (int i = 0; i < count; i++)
                {
                    CrimeRecord record = new CrimeRecord();
                    var legacyHistory = new HeroCrimeStats();
                    SyncRecord(dataStore, i, record, legacyHistory, saving: false);
                    MergeLegacyHistory(legacyHistory);
                    if (!string.IsNullOrWhiteSpace(record.CrimeId) && record.HasOpenCase)
                        _ledger[record.CrimeId] = record;
                }

                int historyCount = 0;
                dataStore.SyncData("gwp_history_count", ref historyCount);
                for (int i = 0; i < historyCount; i++)
                {
                    var history = new HeroCrimeStats();
                    SyncHistory(dataStore, i, history, saving: false);
                    if (!string.IsNullOrWhiteSpace(history.HeroId) && ShouldPersistHistory(history))
                        _history[history.HeroId] = history;
                }

                int taskCount = 0;
                dataStore.SyncData("gwp_ledger_task_count", ref taskCount);
                for (int i = 0; i < taskCount; i++)
                {
                    PoliceTask task = new PoliceTask();
                    SyncTask(dataStore, i, task, saving: false);
                    if (string.IsNullOrWhiteSpace(task.PolicePartyId) ||
                        GetRecordByKey(task.TargetCrimeId) == null ||
                        MobileParty.All.All(p => !string.Equals(p.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    _tasks[task.PolicePartyId] = task;
                }
                TrimOpenCasesToCapacity(MaxTaskPoolEntries);
            }
        }

        private static bool ShouldPersistHistory(HeroCrimeStats history) =>
            !string.IsNullOrWhiteSpace(history.HeroId) &&
            (history.TotalCrimeCount > 0 || history.TotalArrestCount > 0 ||
             history.DirectDeterrencePoints > 0f || history.SharedDeterrencePoints > 0f ||
             history.SharedDeterrenceCount > 0 || history.CaravanArrestCount > 0 ||
             history.CaravanDirectDeterrencePoints > 0f ||
             history.CaravanSharedDeterrencePoints > 0f ||
             history.CaravanSharedDeterrenceCount > 0);

        private static void SyncRecord(
            IDataStore store,
            int i,
            CrimeRecord record,
            HeroCrimeStats legacyHistory,
            bool saving)
        {
            string id = record.CrimeId ?? string.Empty;
            string type = record.CrimeType ?? string.Empty;
            int category = (int)record.CrimeCategory;
            string hero = record.OffenderHeroId ?? string.Empty;
            string party = record.Offender?.StringId ?? record.OffenderPartyId ?? string.Empty;
            float occurred = (float)record.OccurredTime.ToHours;
            float lastCrime = (float)record.LastCrimeTime.ToHours;
            float x = record.Location.X;
            float y = record.Location.Y;
            string victim = record.VictimName ?? string.Empty;
            int crimes = legacyHistory.TotalCrimeCount;
            int arrests = legacyHistory.TotalArrestCount;
            int open = record.HasOpenCase ? 1 : 0;
            float direct = legacyHistory.DirectDeterrencePoints;
            float shared = legacyHistory.SharedDeterrencePoints;
            int sharedCount = legacyHistory.SharedDeterrenceCount;
            float updated = legacyHistory.LastDeterrenceUpdatedHours;
            float enforced = legacyHistory.LastEnforcementHours;

            store.SyncData($"gwp_l_{i}_id", ref id);
            store.SyncData($"gwp_l_{i}_type", ref type);
            store.SyncData($"gwp_l_{i}_category", ref category);
            store.SyncData($"gwp_l_{i}_hero", ref hero);
            store.SyncData($"gwp_l_{i}_party", ref party);
            store.SyncData($"gwp_l_{i}_occurred", ref occurred);
            store.SyncData($"gwp_l_{i}_lastcrime", ref lastCrime);
            store.SyncData($"gwp_l_{i}_x", ref x);
            store.SyncData($"gwp_l_{i}_y", ref y);
            store.SyncData($"gwp_l_{i}_victim", ref victim);
            store.SyncData($"gwp_l_{i}_crimes", ref crimes);
            store.SyncData($"gwp_l_{i}_arrests", ref arrests);
            store.SyncData($"gwp_l_{i}_open", ref open);
            store.SyncData($"gwp_l_{i}_direct", ref direct);
            store.SyncData($"gwp_l_{i}_shared", ref shared);
            store.SyncData($"gwp_l_{i}_sharedcount", ref sharedCount);
            store.SyncData($"gwp_l_{i}_updated", ref updated);
            store.SyncData($"gwp_l_{i}_enforced", ref enforced);

            if (!saving)
            {
                record.CrimeId = id;
                record.CrimeType = type;
                record.CrimeCategory = Enum.IsDefined(typeof(GwpCrimeCategory), category) && category != 0
                    ? (GwpCrimeCategory)category
                    : GwpCrimeCategoryClassifier.FromCrimeType(type, id);
                record.OffenderHeroId = hero;
                record.OffenderPartyId = party;
                record.Offender = id == PlayerCrimeId
                    ? MobileParty.MainParty
                    : MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, party, StringComparison.OrdinalIgnoreCase));
                record.OccurredTime = CampaignTime.Hours(occurred);
                record.LastCrimeTime = CampaignTime.Hours(lastCrime);
                record.Location = new Vec2(x, y);
                record.VictimName = victim;
                record.HasOpenCase = open != 0;

                legacyHistory.HeroId = hero;
                legacyHistory.TotalCrimeCount = Math.Max(0, crimes);
                legacyHistory.TotalArrestCount = Math.Max(0, arrests);
                legacyHistory.DirectDeterrencePoints = MathF.Max(0f, direct);
                legacyHistory.SharedDeterrencePoints = MathF.Max(0f, shared);
                legacyHistory.SharedDeterrenceCount = Math.Max(0, sharedCount);
                legacyHistory.LastDeterrenceUpdatedHours = updated;
                legacyHistory.LastEnforcementHours = enforced;
            }
        }

        private static void SyncHistory(IDataStore store, int i, HeroCrimeStats history, bool saving)
        {
            string hero = history.HeroId ?? string.Empty;
            int crimes = history.TotalCrimeCount;
            int arrests = history.TotalArrestCount;
            float direct = history.DirectDeterrencePoints;
            float shared = history.SharedDeterrencePoints;
            int sharedCount = history.SharedDeterrenceCount;
            float updated = history.LastDeterrenceUpdatedHours;
            float enforced = history.LastEnforcementHours;
            int caravanArrests = history.CaravanArrestCount;
            float caravanDirect = history.CaravanDirectDeterrencePoints;
            float caravanShared = history.CaravanSharedDeterrencePoints;
            int caravanSharedCount = history.CaravanSharedDeterrenceCount;
            float caravanUpdated = history.CaravanLastDeterrenceUpdatedHours;
            float caravanEnforced = history.CaravanLastEnforcementHours;

            store.SyncData($"gwp_h_{i}_hero", ref hero);
            store.SyncData($"gwp_h_{i}_crimes", ref crimes);
            store.SyncData($"gwp_h_{i}_arrests", ref arrests);
            store.SyncData($"gwp_h_{i}_direct", ref direct);
            store.SyncData($"gwp_h_{i}_shared", ref shared);
            store.SyncData($"gwp_h_{i}_sharedcount", ref sharedCount);
            store.SyncData($"gwp_h_{i}_updated", ref updated);
            store.SyncData($"gwp_h_{i}_enforced", ref enforced);
            store.SyncData($"gwp_h_{i}_caravan_arrests", ref caravanArrests);
            store.SyncData($"gwp_h_{i}_caravan_direct", ref caravanDirect);
            store.SyncData($"gwp_h_{i}_caravan_shared", ref caravanShared);
            store.SyncData($"gwp_h_{i}_caravan_sharedcount", ref caravanSharedCount);
            store.SyncData($"gwp_h_{i}_caravan_updated", ref caravanUpdated);
            store.SyncData($"gwp_h_{i}_caravan_enforced", ref caravanEnforced);

            if (!saving)
            {
                history.HeroId = hero;
                history.TotalCrimeCount = Math.Max(0, crimes);
                history.TotalArrestCount = Math.Max(0, arrests);
                history.DirectDeterrencePoints = MathF.Max(0f, direct);
                history.SharedDeterrencePoints = MathF.Max(0f, shared);
                history.SharedDeterrenceCount = Math.Max(0, sharedCount);
                history.LastDeterrenceUpdatedHours = updated;
                history.LastEnforcementHours = enforced;
                history.CaravanArrestCount = Math.Max(0, caravanArrests);
                history.CaravanDirectDeterrencePoints = MathF.Max(0f, caravanDirect);
                history.CaravanSharedDeterrencePoints = MathF.Max(0f, caravanShared);
                history.CaravanSharedDeterrenceCount = Math.Max(0, caravanSharedCount);
                history.CaravanLastDeterrenceUpdatedHours = caravanUpdated;
                history.CaravanLastEnforcementHours = caravanEnforced;
            }
        }

        private static void MergeLegacyHistory(HeroCrimeStats legacy)
        {
            if (!ShouldPersistHistory(legacy)) return;
            if (!_history.TryGetValue(legacy.HeroId, out HeroCrimeStats? current))
            {
                _history[legacy.HeroId] = legacy;
                return;
            }

            current.TotalCrimeCount = Math.Max(current.TotalCrimeCount, legacy.TotalCrimeCount);
            current.TotalArrestCount = Math.Max(current.TotalArrestCount, legacy.TotalArrestCount);
            current.DirectDeterrencePoints = MathF.Max(current.DirectDeterrencePoints, legacy.DirectDeterrencePoints);
            current.SharedDeterrencePoints = MathF.Max(current.SharedDeterrencePoints, legacy.SharedDeterrencePoints);
            current.SharedDeterrenceCount = Math.Max(current.SharedDeterrenceCount, legacy.SharedDeterrenceCount);
            current.LastDeterrenceUpdatedHours = MathF.Max(current.LastDeterrenceUpdatedHours, legacy.LastDeterrenceUpdatedHours);
            current.LastEnforcementHours = MathF.Max(current.LastEnforcementHours, legacy.LastEnforcementHours);
            current.CaravanArrestCount = Math.Max(current.CaravanArrestCount,
                legacy.CaravanArrestCount);
            current.CaravanDirectDeterrencePoints = MathF.Max(
                current.CaravanDirectDeterrencePoints, legacy.CaravanDirectDeterrencePoints);
            current.CaravanSharedDeterrencePoints = MathF.Max(
                current.CaravanSharedDeterrencePoints, legacy.CaravanSharedDeterrencePoints);
            current.CaravanSharedDeterrenceCount = Math.Max(
                current.CaravanSharedDeterrenceCount, legacy.CaravanSharedDeterrenceCount);
            current.CaravanLastDeterrenceUpdatedHours = MathF.Max(
                current.CaravanLastDeterrenceUpdatedHours,
                legacy.CaravanLastDeterrenceUpdatedHours);
            current.CaravanLastEnforcementHours = MathF.Max(
                current.CaravanLastEnforcementHours, legacy.CaravanLastEnforcementHours);
        }

        private static void SyncTask(IDataStore store, int i, PoliceTask task, bool saving)
        {
            string police = task.PolicePartyId ?? string.Empty;
            string target = task.TargetCrimeId ?? string.Empty;
            int war = task.WarDeclared ? 1 : 0;
            string warTarget = task.WarTarget?.StringId ?? string.Empty;
            int escort = task.IsEscortingPlayer ? 1 : 0;
            int prep = task.IsPreparingDispatch ? 1 : 0;
            string settlement = task.EscortSettlement?.StringId ?? string.Empty;
            int bounty = task.IsPlayerBountyEscort ? 1 : 0;
            float leaderSoloSpeed =
                MathF.Max(0f, task.LeaderSoloSpeedAtAssignment);
            int theoreticalLeaderSoloSpeed =
                task.HasTheoreticalLeaderSoloSpeedAtAssignment ? 1 : 0;

            store.SyncData($"gwp_lt_{i}_police", ref police);
            store.SyncData($"gwp_lt_{i}_target", ref target);
            store.SyncData($"gwp_lt_{i}_war", ref war);
            store.SyncData($"gwp_lt_{i}_wartarget", ref warTarget);
            store.SyncData($"gwp_lt_{i}_escort", ref escort);
            store.SyncData($"gwp_lt_{i}_prep", ref prep);
            store.SyncData($"gwp_lt_{i}_settlement", ref settlement);
            store.SyncData($"gwp_lt_{i}_bounty", ref bounty);
            store.SyncData($"gwp_lt_{i}_leader_solo_speed",
                ref leaderSoloSpeed);
            store.SyncData($"gwp_lt_{i}_leader_solo_speed_theoretical",
                ref theoreticalLeaderSoloSpeed);

            if (!saving)
            {
                task.PolicePartyId = police;
                task.TargetCrimeId = target;
                task.WarDeclared = war != 0;
                task.WarTarget = string.IsNullOrWhiteSpace(warTarget)
                    ? null
                    : (IFaction?)Kingdom.All.FirstOrDefault(k => k.StringId == warTarget)
                      ?? Clan.All.FirstOrDefault(c => c.StringId == warTarget);
                task.IsEscortingPlayer = escort != 0;
                task.IsPreparingDispatch = prep != 0;
                task.EscortSettlement = string.IsNullOrWhiteSpace(settlement)
                    ? null
                    : Settlement.FindFirst(s => s.StringId == settlement);
                task.IsPlayerBountyEscort = bounty != 0;
                task.LeaderSoloSpeedAtAssignment =
                    MathF.Max(0f, leaderSoloSpeed);
                task.HasTheoreticalLeaderSoloSpeedAtAssignment =
                    theoreticalLeaderSoloSpeed != 0;
            }
        }
    }

    // ===================== 玩家行为数据 =====================
    public class PlayerRecord
    {
        public string Type { get; set; } = string.Empty;
        public bool IsCrime { get; set; }
        public CampaignTime Time { get; set; }
        public Vec2 Location { get; set; }
        public string Detail { get; set; } = string.Empty;
    }

    /// <summary>
    /// 玩家行为池 - 记录玩家的犯罪和行善行为
    /// 声望：正数=好人，0=中立，负数=坏人（≤-5触发警察追捕，-1~-4触发纠察队）
    /// </summary>
    public static class PlayerBehaviorPool
    {
        private static readonly List<PlayerRecord> _records = new List<PlayerRecord>();
        private static readonly HashSet<IFaction> _victimFactions = new HashSet<IFaction>();

        public const int MaxReputation =  100; // 声望上限
        public const int MinReputation = -100; // 声望下限

        public static int Reputation { get; private set; } = 0;
        public static int GoodDeedKillProgress { get; private set; } = 0;
        public static bool IsWanted => Reputation <= -11;
        public static bool HasAtonementTask { get; private set; } = false;
        public static IReadOnlyList<PlayerRecord> Records => _records;
        public static IReadOnlyCollection<IFaction> VictimFactions => _victimFactions;

        public static void ClearVictimFactions() => _victimFactions.Clear();

        public static void AddVictimFactionOnLoad(IFaction faction)
        {
            if (faction != null) _victimFactions.Add(faction);
        }

        public static void AddCrime(string type, Vec2 location, string detail, IFaction? victimFaction = null)
        {
            _records.Add(new PlayerRecord { Type = type, IsCrime = true, Time = CampaignTime.Now, Location = location, Detail = detail });
            Reputation = Math.Max(Reputation - 1, MinReputation);

            if (victimFaction != null) _victimFactions.Add(victimFaction);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_gwpdata_003}The Grey Wardens have recorded your crimes: {VAR_1} ({VAR_2}) | {VAR_3}", "VAR_1", type, "VAR_2", detail, "VAR_3", GetReputationDisplay()), Colors.Red));

            if (IsWanted)
                CrimePool.TryAddPlayerCrime(type, location, detail);
        }

        /// <summary>
        /// 仅记录犯罪事件（历史记录 + 受害势力追踪），不扣声望、不弹通知、不触发警察追捕。
        /// 用于"按战斗人数缩放扣声望"场景：调用方在战斗结束后（OnMapEventEnded）统一处理声望。
        /// </summary>
        public static void AddCrimeRecord(string type, Vec2 location, string detail, IFaction? victimFaction = null)
        {
            _records.Add(new PlayerRecord { Type = type, IsCrime = true, Time = CampaignTime.Now, Location = location, Detail = detail });
            if (victimFaction != null) _victimFactions.Add(victimFaction);
            // 不扣声望、不弹通知：声望扣除由 OnMapEventEnded 按击败人数缩放执行
        }

        public static void AddGoodDeed(string type, Vec2 location, string detail)
        {
            _records.Add(new PlayerRecord { Type = type, IsCrime = false, Time = CampaignTime.Now, Location = location, Detail = detail });
            Reputation = Math.Min(Reputation + 1, MaxReputation);

            InformationManager.DisplayMessage(new InformationMessage(
                GwpText.Get("{=gwp_gwpdata_004}The Grey Wardens have noticed your good deeds: {VAR_1} ({VAR_2}) | {VAR_3}", "VAR_1", type, "VAR_2", detail, "VAR_3", GetReputationDisplay()), Colors.Green));
        }

        public static string GetReputationDisplay() => GwpText.Get("{=gwp_gwpdata_005}Reputation: {VAR_1}", "VAR_1", Reputation);

        public static void ResetReputation(int value) => Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, value));
        public static void ResetGoodDeedKillProgress(int value) => GoodDeedKillProgress = Math.Max(0, Math.Min(9, value));

        public static int AccumulateGoodDeedKills(int killCount)
        {
            if (killCount <= 0) return 0;

            int accumulated = GoodDeedKillProgress + killCount;
            int reputationGain = accumulated / 10;
            GoodDeedKillProgress = accumulated % 10;
            return reputationGain;
        }
        public static void ChangeReputation(int delta) => Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, Reputation + delta));
        public static void SetAtonementTaskActive(bool active) => HasAtonementTask = active;

        public static void ClearAll()
        {
            Reputation = 0;
            GoodDeedKillProgress = 0;
            HasAtonementTask = false;
            _records.Clear();
            _victimFactions.Clear();
        }
    }
}
