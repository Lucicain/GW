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

    /// <summary>
    /// 每位领主唯一的一条永久案底账本。案件状态、累计犯罪/被捕次数与两类威慑共用此记录，
    /// 避免把同一事件同时复制到犯罪池、任务和威慑表。
    /// </summary>
    public class CrimeRecord
    {
        private MobileParty? _offender;
        private Hero? _offenderHero;

        public string CrimeId { get; set; } = string.Empty;
        public string CrimeType { get; set; } = string.Empty;
        public string OffenderHeroId { get; set; } = string.Empty;
        public string OffenderPartyId { get; set; } = string.Empty;
        public CampaignTime OccurredTime { get; set; }
        public CampaignTime LastCrimeTime { get; set; }
        public Vec2 Location { get; set; }
        public string VictimName { get; set; } = string.Empty;
        public int TotalCrimeCount { get; set; }
        public int TotalArrestCount { get; set; }
        public bool HasOpenCase { get; set; }
        public float DirectDeterrencePoints { get; set; }
        public float SharedDeterrencePoints { get; set; }
        public int SharedDeterrenceCount { get; set; }
        public float LastDeterrenceUpdatedHours { get; set; }
        public float LastEnforcementHours { get; set; }

        public Hero? OffenderHero
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OffenderHeroId)) return null;
                if (_offenderHero != null &&
                    string.Equals(_offenderHero.StringId, OffenderHeroId, StringComparison.OrdinalIgnoreCase))
                    return _offenderHero;

                _offenderHero = Hero.FindFirst(h =>
                    string.Equals(h.StringId, OffenderHeroId, StringComparison.OrdinalIgnoreCase));
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

    /// <summary>只保存警务流程状态；目标案情通过账本键实时解析，不再复制整条犯罪记录。</summary>
    public class PoliceTask
    {
        public string PolicePartyId { get; set; } = string.Empty;
        public string TargetCrimeId { get; set; } = string.Empty;
        public CrimeRecord? TargetCrime
        {
            get => CrimePool.GetRecordByKey(TargetCrimeId);
            set => TargetCrimeId = value?.CrimeId ?? string.Empty;
        }

        public bool WarDeclared { get; set; }
        public IFaction? WarTarget { get; set; }
        public bool IsEscortingPlayer { get; set; }
        public bool IsPreparingDispatch { get; set; }
        public Settlement? EscortSettlement { get; set; }
        public bool IsPlayerBountyEscort { get; set; }

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
    }

    /// <summary>
    /// 按领主聚合的永久案底账本。每名领主最多一条记录；未结案件只是记录上的一个状态，
    /// 警察任务只保存账本键，因此存档大小随领主数量而不是犯罪事件数量增长。
    /// </summary>
    public static class CrimePool
    {
        private static readonly Dictionary<string, CrimeRecord> _ledger =
            new Dictionary<string, CrimeRecord>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, PoliceTask> _tasks =
            new Dictionary<string, PoliceTask>(StringComparer.OrdinalIgnoreCase);

        public const string PlayerCrimeId = "PLAYER_WANTED";

        public static bool IsAccepting => true;
        public static bool IsDispatchReady => GetUnassignedOpenCases().Any(c => c.IsOffenderPursuable());
        public static IReadOnlyDictionary<string, PoliceTask> ActiveTasks => _tasks;
        public static IEnumerable<CrimeRecord> LedgerRecords => _ledger.Values;

        public static void ClearAll()
        {
            _ledger.Clear();
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

        public static CrimeRecord GetOrCreateRecord(Hero hero)
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
                LastCrimeTime = CampaignTime.Now,
                LastDeterrenceUpdatedHours = (float)CampaignTime.Now.ToHours
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
            if (playerParty == null || !playerParty.IsActive || IsPlayerHunted) return false;

            _ledger[PlayerCrimeId] = new CrimeRecord
            {
                CrimeId = PlayerCrimeId,
                CrimeType = crimeType,
                Offender = playerParty,
                OccurredTime = CampaignTime.Now,
                LastCrimeTime = CampaignTime.Now,
                Location = location,
                VictimName = detail,
                TotalCrimeCount = 1,
                HasOpenCase = true
            };

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

            CrimeRecord record = GetOrCreateRecord(leader);
            CampaignTime now = CampaignTime.Now;
            if (!record.HasOpenCase)
                record.OccurredTime = now;

            record.CrimeType = crimeType ?? string.Empty;
            record.Offender = offender;
            record.LastCrimeTime = now;
            record.Location = location;
            record.VictimName = victimName ?? string.Empty;
            record.TotalCrimeCount++;
            record.HasOpenCase = true;
            return true;
        }

        /// <summary>任务失败时只重开原案，不增加永久犯罪次数。</summary>
        public static void ReopenCase(CrimeRecord? crime)
        {
            if (crime == null || crime.CrimeId == PlayerCrimeId) return;
            crime.HasOpenCase = true;
        }

        public static int RecordArrest(Hero? hero)
        {
            if (hero == null || hero == Hero.MainHero || string.IsNullOrWhiteSpace(hero.StringId))
                return 0;

            CrimeRecord record = GetOrCreateRecord(hero);
            record.TotalArrestCount++;
            record.HasOpenCase = false;
            return record.TotalArrestCount;
        }

        public static CrimeRecord? GetNearest(Vec2 pos) => SelectNearest(
            GetUnassignedOpenCases().Where(c => c.IsOffenderPursuable()), pos);

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
            record.HasOpenCase = false;
            return true;
        }

        public static void BeginTask(string policePartyId, CrimeRecord crime)
        {
            if (string.IsNullOrWhiteSpace(policePartyId) || crime == null) return;
            _tasks[policePartyId] = new PoliceTask
            {
                PolicePartyId = policePartyId,
                TargetCrimeId = crime.CrimeId
            };
        }

        /// <summary>结束任务即结案；失败分支随后调用 ReopenCase 恢复原案。</summary>
        public static void EndTask(string policePartyId)
        {
            if (_tasks.TryGetValue(policePartyId, out PoliceTask? task))
            {
                CrimeRecord? crime = task.TargetCrime;
                _tasks.Remove(policePartyId);
                if (crime != null && crime.CrimeId != PlayerCrimeId)
                    crime.HasOpenCase = false;
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
                if (_tasks.TryGetValue(policeId, out PoliceTask? task))
                    ReopenCase(task.TargetCrime);
                _tasks.Remove(policeId);
            }

            foreach (CrimeRecord record in _ledger.Values)
            {
                Hero? hero = record.OffenderHero;
                if (record.CrimeId != PlayerCrimeId && hero != null && !hero.IsAlive)
                    record.HasOpenCase = false;
            }
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
                List<CrimeRecord> records = _ledger.Values.Where(ShouldPersist).ToList();
                int count = records.Count;
                dataStore.SyncData("gwp_ledger_count", ref count);
                for (int i = 0; i < records.Count; i++)
                    SyncRecord(dataStore, i, records[i], saving: true);

                List<PoliceTask> tasks = _tasks.Values.Where(t => t.TargetCrime != null).ToList();
                int taskCount = tasks.Count;
                dataStore.SyncData("gwp_ledger_task_count", ref taskCount);
                for (int i = 0; i < tasks.Count; i++)
                    SyncTask(dataStore, i, tasks[i], saving: true);
            }
            else if (dataStore.IsLoading)
            {
                _ledger.Clear();
                _tasks.Clear();

                int count = 0;
                dataStore.SyncData("gwp_ledger_count", ref count);
                for (int i = 0; i < count; i++)
                {
                    CrimeRecord record = new CrimeRecord();
                    SyncRecord(dataStore, i, record, saving: false);
                    if (!string.IsNullOrWhiteSpace(record.CrimeId))
                        _ledger[record.CrimeId] = record;
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

                Clean();
            }
        }

        private static bool ShouldPersist(CrimeRecord record) =>
            record.CrimeId == PlayerCrimeId || record.TotalCrimeCount > 0 || record.TotalArrestCount > 0 ||
            record.HasOpenCase || record.DirectDeterrencePoints > 0f || record.SharedDeterrencePoints > 0f;

        private static void SyncRecord(IDataStore store, int i, CrimeRecord record, bool saving)
        {
            string id = record.CrimeId ?? string.Empty;
            string type = record.CrimeType ?? string.Empty;
            string hero = record.OffenderHeroId ?? string.Empty;
            string party = record.Offender?.StringId ?? record.OffenderPartyId ?? string.Empty;
            float occurred = (float)record.OccurredTime.ToHours;
            float lastCrime = (float)record.LastCrimeTime.ToHours;
            float x = record.Location.X;
            float y = record.Location.Y;
            string victim = record.VictimName ?? string.Empty;
            int crimes = record.TotalCrimeCount;
            int arrests = record.TotalArrestCount;
            int open = record.HasOpenCase ? 1 : 0;
            float direct = record.DirectDeterrencePoints;
            float shared = record.SharedDeterrencePoints;
            int sharedCount = record.SharedDeterrenceCount;
            float updated = record.LastDeterrenceUpdatedHours;
            float enforced = record.LastEnforcementHours;

            store.SyncData($"gwp_l_{i}_id", ref id);
            store.SyncData($"gwp_l_{i}_type", ref type);
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
                record.OffenderHeroId = hero;
                record.OffenderPartyId = party;
                record.Offender = id == PlayerCrimeId
                    ? MobileParty.MainParty
                    : MobileParty.All.FirstOrDefault(p => string.Equals(p.StringId, party, StringComparison.OrdinalIgnoreCase));
                record.OccurredTime = CampaignTime.Hours(occurred);
                record.LastCrimeTime = CampaignTime.Hours(lastCrime);
                record.Location = new Vec2(x, y);
                record.VictimName = victim;
                record.TotalCrimeCount = Math.Max(0, crimes);
                record.TotalArrestCount = Math.Max(0, arrests);
                record.HasOpenCase = open != 0;
                record.DirectDeterrencePoints = MathF.Max(0f, direct);
                record.SharedDeterrencePoints = MathF.Max(0f, shared);
                record.SharedDeterrenceCount = Math.Max(0, sharedCount);
                record.LastDeterrenceUpdatedHours = updated;
                record.LastEnforcementHours = enforced;
            }
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

            store.SyncData($"gwp_lt_{i}_police", ref police);
            store.SyncData($"gwp_lt_{i}_target", ref target);
            store.SyncData($"gwp_lt_{i}_war", ref war);
            store.SyncData($"gwp_lt_{i}_wartarget", ref warTarget);
            store.SyncData($"gwp_lt_{i}_escort", ref escort);
            store.SyncData($"gwp_lt_{i}_prep", ref prep);
            store.SyncData($"gwp_lt_{i}_settlement", ref settlement);
            store.SyncData($"gwp_lt_{i}_bounty", ref bounty);

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
