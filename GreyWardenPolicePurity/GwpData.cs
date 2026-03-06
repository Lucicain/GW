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

    public class CrimeRecord
    {
        public string CrimeId { get; set; } = string.Empty;
        public string CrimeType { get; set; } = string.Empty;
        public MobileParty? Offender { get; set; }
        public CampaignTime OccurredTime { get; set; }
        public Vec2 Location { get; set; }
        public string VictimName { get; set; } = string.Empty;

        public bool IsOffenderValid() => Offender?.IsActive == true;
        public bool IsOffenderPursuable() => Offender?.IsActive == true && Offender.CurrentSettlement == null;
    }

    public class PoliceTask
    {
        public string PolicePartyId { get; set; } = string.Empty;
        public CrimeRecord? TargetCrime { get; set; }
        public bool WarDeclared { get; set; }
        public IFaction? WarTarget { get; set; }
        public bool IsEscortingPlayer { get; set; }
        /// <summary>押送目标城镇（俘获玩家时记录，避免每帧重新计算导致目标漂移）</summary>
        public Settlement? EscortSettlement { get; set; }
        /// <summary>
        /// 是否正在为玩家悬赏任务护送（跟随玩家追击目标）。
        /// true 时 PoliceEnforcementBehavior.UpdateTasks() 完全跳过此任务，
        /// 由 PlayerBountyBehavior.UpdateEscortPatrol() 接管 AI 命令。
        /// </summary>
        public bool IsPlayerBountyEscort { get; set; }

        public bool IsTargetValid() => TargetCrime?.IsOffenderValid() == true;
    }

    /// <summary>警察家族统计工具</summary>
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
                .Where(w =>
                    w?.MobileParty != null &&
                    w.MobileParty.IsActive &&
                    !GwpCommon.IsEnforcementDelayPatrolParty(w.MobileParty))
                .Select(w => w.MobileParty)
                .ToList();
        }

        public static int PartyCount => GetAllPoliceParties().Count;
        public static int MaxActiveTasks => PartyCount;
    }

    /// <summary>
    /// 犯罪池管理
    /// 池容量 = 空闲警察数量；每小时自动清理无效记录
    /// </summary>
    public static class CrimePool
    {
        private static readonly List<CrimeRecord> _pool = new List<CrimeRecord>();
        private static readonly Dictionary<string, PoliceTask> _tasks = new Dictionary<string, PoliceTask>();
        private const string PlayerCrimeId = "PLAYER_WANTED";

        public static void ClearAll()
        {
            _pool.Clear();
            _tasks.Clear();
            IsAccepting = true;
        }

        public static bool IsAccepting { get; private set; } = true;
        public static IReadOnlyDictionary<string, PoliceTask> ActiveTasks => _tasks;

        /// <summary>玩家是否正在被通缉（池中或任务中有玩家犯罪）</summary>
        public static bool IsPlayerHunted
        {
            get
            {
                if (_pool.Any(c => c.IsOffenderValid() && c.Offender.IsMainParty)) return true;
                if (_tasks.Values.Any(t => t.IsTargetValid() && t.TargetCrime.Offender.IsMainParty)) return true;
                return false;
            }
        }

        /// <summary>将玩家加入通缉池（无视容量限制）</summary>
        public static bool TryAddPlayerCrime(string crimeType, Vec2 location, string detail)
        {
            MobileParty playerParty = MobileParty.MainParty;
            if (playerParty == null || !playerParty.IsActive) return false;
            if (IsPlayerHunted) return false;

            _pool.Add(new CrimeRecord
            {
                CrimeId = PlayerCrimeId,
                CrimeType = crimeType,
                Offender = playerParty,
                OccurredTime = CampaignTime.Now,
                Location = location,
                VictimName = detail
            });

            InformationManager.DisplayMessage(new InformationMessage(
                "你已被灰袍守卫列入通缉名单！", Colors.Red));
            return true;
        }

        public static void EndPlayerHunt()
        {
            _pool.RemoveAll(c => c.CrimeId == PlayerCrimeId);

            var playerTaskKeys = _tasks
                .Where(kvp => kvp.Value.TargetCrime?.Offender?.IsMainParty == true)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in playerTaskKeys)
                _tasks.Remove(key);

            InformationManager.DisplayMessage(new InformationMessage(
                "通缉已解除，灰袍守卫不再追捕你", Colors.Green));
            RefreshAccepting();
        }

        /// <summary>获取当前玩家通缉记录（可能在待处理池，也可能在活跃任务中）。</summary>
        public static CrimeRecord? GetPlayerCrime()
        {
            CrimeRecord? poolCrime = _pool.FirstOrDefault(c => c.CrimeId == PlayerCrimeId || c.Offender?.IsMainParty == true);
            if (poolCrime != null) return poolCrime;

            foreach (PoliceTask task in _tasks.Values)
            {
                if (task.TargetCrime?.Offender?.IsMainParty == true)
                    return task.TargetCrime;
            }

            return null;
        }

        /// <summary>获取当前负责追捕玩家的警察部队 ID（无则 null）。</summary>
        public static string? GetPlayerTaskPolicePartyId()
        {
            foreach (var kv in _tasks)
            {
                if (kv.Value.TargetCrime?.Offender?.IsMainParty == true)
                    return kv.Key;
            }
            return null;
        }

        public static bool TryAdd(string crimeType, MobileParty offender, Vec2 location, string victimName)
        {
            if (offender == null || !offender.IsActive) return false;
            if (!IsAccepting) return false;

            string offenderId = offender.StringId;
            if (_pool.Any(c => c.Offender?.StringId == offenderId)) return false;
            if (_tasks.Values.Any(t => t.TargetCrime?.Offender?.StringId == offenderId)) return false;

            _pool.Add(new CrimeRecord
            {
                CrimeId = $"C_{(long)CampaignTime.Now.ToHours}_{MBRandom.RandomInt(1000, 9999)}",
                CrimeType = crimeType,
                Offender = offender,
                OccurredTime = CampaignTime.Now,
                Location = location,
                VictimName = victimName
            });
            RefreshAccepting();
            return true;
        }

        public static void Return(CrimeRecord? crime)
        {
            if (crime != null && crime.IsOffenderValid())
                _pool.Add(crime);
        }

        public static CrimeRecord? GetNearest(Vec2 pos)
        {
            CrimeRecord? best = null;
            float bestDist = float.MaxValue;
            foreach (var c in _pool)
            {
                if (!c.IsOffenderPursuable()) continue;
                float d = pos.Distance(c.Offender.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        public static CrimeRecord? GetNearestNonPlayer(Vec2 pos)
        {
            CrimeRecord? best = null;
            float bestDist = float.MaxValue;
            foreach (var c in _pool)
            {
                if (!c.IsOffenderPursuable()) continue;
                if (c.Offender.IsMainParty) continue;
                float d = pos.Distance(c.Offender.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        /// <summary>从待处理池 + 活跃任务中查找（悬赏猎人用）</summary>
        public static CrimeRecord? GetNearestNonPlayerFromAll(Vec2 pos)
        {
            CrimeRecord? best = null;
            float bestDist = float.MaxValue;

            foreach (var c in _pool)
            {
                if (!c.IsOffenderPursuable()) continue;
                if (c.Offender.IsMainParty) continue;
                float d = pos.Distance(c.Offender.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            foreach (var task in _tasks.Values)
            {
                var crime = task.TargetCrime;
                if (crime == null || !crime.IsOffenderPursuable()) continue;
                if (crime.Offender.IsMainParty) continue;
                float d = pos.Distance(crime.Offender.GetPosition2D);
                if (d < bestDist) { bestDist = d; best = crime; }
            }
            return best;
        }

        /// <summary>
        /// 查找已被分配追捕指定罪犯的警察部队 StringId。
        /// 若无警察部队持有该任务（犯罪仍在 _pool 中），返回 null。
        /// 供玩家接悬赏时确定护送方使用。
        /// </summary>
        public static string? GetAssignedPolicePartyId(string? offenderStringId)
        {
            if (string.IsNullOrEmpty(offenderStringId)) return null;
            foreach (var kv in _tasks)
                if (kv.Value.TargetCrime?.Offender?.StringId == offenderStringId)
                    return kv.Key; // key = policePartyId
            return null;
        }

        /// <summary>
        /// 标记/取消标记警察部队的玩家悬赏护送状态。
        /// 标记后 PoliceEnforcementBehavior.UpdateTasks() 跳过该部队，
        /// 完全由 PlayerBountyBehavior.UpdateEscortPatrol() 管理其 AI。
        /// </summary>
        public static void SetBountyEscortFlag(string policePartyId, bool value)
        {
            if (string.IsNullOrEmpty(policePartyId)) return;
            if (_tasks.TryGetValue(policePartyId, out var task))
                task.IsPlayerBountyEscort = value;
        }

        /// <summary>按犯罪者 StringId 查找（悬赏通知点击时用）</summary>
        public static CrimeRecord? GetByOffenderId(string? partyStringId)
        {
            if (partyStringId == null) return null;
            foreach (var c in _pool)
                if (c.IsOffenderValid() && c.Offender.StringId == partyStringId) return c;
            foreach (var task in _tasks.Values)
                if (task.TargetCrime?.IsOffenderValid() == true &&
                    task.TargetCrime.Offender.StringId == partyStringId) return task.TargetCrime;
            return null;
        }

        public static void BeginTask(string policePartyId, CrimeRecord crime)
        {
            _tasks[policePartyId] = new PoliceTask
            {
                PolicePartyId = policePartyId,
                TargetCrime = crime,
                WarDeclared = false,
                WarTarget = null,
                IsEscortingPlayer = false
            };
            _pool.Remove(crime);
            RefreshAccepting();
        }

        public static void EndTask(string policePartyId)
        {
            _tasks.Remove(policePartyId);
            RefreshAccepting();
        }

        public static PoliceTask? GetTask(string policePartyId)
        {
            _tasks.TryGetValue(policePartyId, out var task);
            return task;
        }

        public static bool HasTask(string policePartyId) => _tasks.ContainsKey(policePartyId);

        /// <summary>
        /// 强制把玩家通缉案件分配给指定警察部队。
        /// 若指定部队已有其他任务，会把该任务的犯罪记录退回池子。
        /// 若玩家案件已在其他部队手中，会转移到指定部队。
        /// </summary>
        public static bool TryAssignPlayerCrimeToPolice(string policePartyId)
        {
            if (string.IsNullOrEmpty(policePartyId)) return false;

            CrimeRecord? playerCrime = GetPlayerCrime();
            if (playerCrime == null || playerCrime.Offender?.IsMainParty != true) return false;

            if (_tasks.TryGetValue(policePartyId, out PoliceTask samePartyTask) &&
                samePartyTask.TargetCrime?.Offender?.IsMainParty == true)
            {
                return true;
            }

            // 先把玩家案件从池中清掉（后续由指定部队接手）
            _pool.RemoveAll(c => c.CrimeId == PlayerCrimeId || c.Offender?.IsMainParty == true);

            // 移除旧的玩家追捕任务（若存在）
            string? oldPlayerTaskPartyId = GetPlayerTaskPolicePartyId();
            if (!string.IsNullOrEmpty(oldPlayerTaskPartyId))
                _tasks.Remove(oldPlayerTaskPartyId);

            // 指定部队已有非玩家任务时，案件归池（避免丢案）
            if (_tasks.TryGetValue(policePartyId, out PoliceTask displacedTask))
            {
                CrimeRecord? displacedCrime = displacedTask.TargetCrime;
                if (displacedCrime?.IsOffenderValid() == true &&
                    displacedCrime.Offender?.IsMainParty != true &&
                    !_pool.Any(c => c.Offender?.StringId == displacedCrime.Offender?.StringId))
                {
                    _pool.Add(displacedCrime);
                }

                _tasks.Remove(policePartyId);
            }

            _tasks[policePartyId] = new PoliceTask
            {
                PolicePartyId = policePartyId,
                TargetCrime = playerCrime,
                WarDeclared = false,
                WarTarget = null,
                IsEscortingPlayer = false,
                EscortSettlement = null,
                IsPlayerBountyEscort = false
            };

            RefreshAccepting();
            return true;
        }

        public static void Clean() => _pool.RemoveAll(c => !c.IsOffenderValid());

        /// <summary>
        /// 获取犯罪池（待处理 + 已分配任务）中属于指定势力的活跃罪犯队伍。
        /// 用于执法超时时判断是否仍有可追捕对象。
        /// </summary>
        public static List<MobileParty> GetTrackedOffendersByFaction(IFaction? faction)
        {
            var result = new List<MobileParty>();
            if (faction == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(MobileParty? offender)
            {
                if (offender == null || !offender.IsActive || offender.IsMainParty) return;
                IFaction offenderFaction = offender.MapFaction;
                if (offenderFaction == null || offenderFaction != faction) return;
                if (string.IsNullOrEmpty(offender.StringId)) return;
                if (!seen.Add(offender.StringId)) return;
                result.Add(offender);
            }

            foreach (var c in _pool)
                TryAdd(c.Offender);

            foreach (var t in _tasks.Values)
                TryAdd(t.TargetCrime?.Offender);

            return result;
        }

        public static bool HasTrackedOffenderByFaction(IFaction? faction) =>
            GetTrackedOffendersByFaction(faction).Count > 0;

        /// <summary>
        /// 获取犯罪池（待处理 + 已分配任务）中的全部活跃罪犯（可选是否包含玩家）。
        /// 用于读档后恢复纠察支援队目标时进行就近匹配。
        /// </summary>
        public static List<MobileParty> GetAllTrackedOffenders(bool includePlayer = false)
        {
            var result = new List<MobileParty>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void TryAdd(MobileParty? offender)
            {
                if (offender == null || !offender.IsActive) return;
                if (!includePlayer && offender.IsMainParty) return;
                if (string.IsNullOrEmpty(offender.StringId)) return;
                if (!seen.Add(offender.StringId)) return;
                result.Add(offender);
            }

            foreach (var c in _pool)
                TryAdd(c.Offender);

            foreach (var t in _tasks.Values)
                TryAdd(t.TargetCrime?.Offender);

            return result;
        }

        public static void RefreshAccepting()
        {
            int freePolice = PoliceStats.PartyCount - _tasks.Count;
            int poolCount = _pool.Count(c => c.IsOffenderValid());
            IsAccepting = poolCount < freePolice;
        }

        /// <summary>
        /// 将 CrimePool 的运行时状态序列化到存档，或从存档恢复。
        /// 由 PoliceEnforcementBehavior.SyncData() 调用。
        ///
        /// MobileParty / IFaction / Settlement 均以 StringId 存储，读档时重新查找活体引用。
        /// 若目标或警察部队已消失（散伙、被击败），对应条目自动跳过（静默清理）。
        /// </summary>
        public static void SyncData(IDataStore dataStore)
        {
            if (dataStore.IsSaving)
            {
                // ── 保存 _pool（未分配任务的待处理犯罪）──────────────────────────────
                var validPool = _pool.Where(c => c.Offender != null).ToList();
                int poolCount = validPool.Count;
                dataStore.SyncData("gwp_cp_pool_count", ref poolCount);
                for (int i = 0; i < validPool.Count; i++)
                {
                    var c = validPool[i];
                    string cid   = c.CrimeId    ?? "";
                    string ctype = c.CrimeType  ?? "";
                    string coffe = c.Offender?.StringId ?? "";
                    float  ctime = (float)c.OccurredTime.ToHours;
                    float  clocx = c.Location.X;
                    float  clocy = c.Location.Y;
                    string cvict = c.VictimName ?? "";
                    dataStore.SyncData($"gwp_cp_p{i}_id",     ref cid);
                    dataStore.SyncData($"gwp_cp_p{i}_type",   ref ctype);
                    dataStore.SyncData($"gwp_cp_p{i}_offen",  ref coffe);
                    dataStore.SyncData($"gwp_cp_p{i}_time",   ref ctime);
                    dataStore.SyncData($"gwp_cp_p{i}_locx",   ref clocx);
                    dataStore.SyncData($"gwp_cp_p{i}_locy",   ref clocy);
                    dataStore.SyncData($"gwp_cp_p{i}_victim", ref cvict);
                }

                // ── 保存 _tasks（已分配给警察部队的犯罪，crime 嵌入在 task 中）──────
                var taskList = _tasks.Values.ToList();
                int taskCount = taskList.Count;
                dataStore.SyncData("gwp_cp_task_count", ref taskCount);
                for (int i = 0; i < taskList.Count; i++)
                {
                    var t = taskList[i];
                    var c = t.TargetCrime;
                    string tpol  = t.PolicePartyId ?? "";
                    string tcid  = c?.CrimeId    ?? "";
                    string tctype= c?.CrimeType  ?? "";
                    string tcoffe= c?.Offender?.StringId ?? "";
                    float  tctime= c != null ? (float)c.OccurredTime.ToHours : 0f;
                    float  tclocx= c?.Location.X ?? 0f;
                    float  tclocy= c?.Location.Y ?? 0f;
                    string tcvict= c?.VictimName ?? "";
                    int    twar  = t.WarDeclared       ? 1 : 0;
                    string twt   = t.WarTarget?.StringId ?? "";
                    int    tesc  = t.IsEscortingPlayer  ? 1 : 0;
                    string tescs = t.EscortSettlement?.StringId ?? "";
                    int    tbe   = t.IsPlayerBountyEscort ? 1 : 0;
                    dataStore.SyncData($"gwp_cp_t{i}_police",  ref tpol);
                    dataStore.SyncData($"gwp_cp_t{i}_cid",     ref tcid);
                    dataStore.SyncData($"gwp_cp_t{i}_ctype",   ref tctype);
                    dataStore.SyncData($"gwp_cp_t{i}_coffen",  ref tcoffe);
                    dataStore.SyncData($"gwp_cp_t{i}_ctime",   ref tctime);
                    dataStore.SyncData($"gwp_cp_t{i}_clocx",   ref tclocx);
                    dataStore.SyncData($"gwp_cp_t{i}_clocy",   ref tclocy);
                    dataStore.SyncData($"gwp_cp_t{i}_cvictim", ref tcvict);
                    dataStore.SyncData($"gwp_cp_t{i}_war",     ref twar);
                    dataStore.SyncData($"gwp_cp_t{i}_wt",      ref twt);
                    dataStore.SyncData($"gwp_cp_t{i}_esc",     ref tesc);
                    dataStore.SyncData($"gwp_cp_t{i}_escs",    ref tescs);
                    dataStore.SyncData($"gwp_cp_t{i}_be",      ref tbe);
                }
            }
            else if (dataStore.IsLoading)
            {
                _pool.Clear();
                _tasks.Clear();
                IsAccepting = true;

                // ── 恢复 _pool ─────────────────────────────────────────────────────
                int poolCount = 0;
                dataStore.SyncData("gwp_cp_pool_count", ref poolCount);
                for (int i = 0; i < poolCount; i++)
                {
                    string cid = ""; string ctype = ""; string coffe = "";
                    float ctime = 0f; float clocx = 0f; float clocy = 0f; string cvict = "";
                    dataStore.SyncData($"gwp_cp_p{i}_id",     ref cid);
                    dataStore.SyncData($"gwp_cp_p{i}_type",   ref ctype);
                    dataStore.SyncData($"gwp_cp_p{i}_offen",  ref coffe);
                    dataStore.SyncData($"gwp_cp_p{i}_time",   ref ctime);
                    dataStore.SyncData($"gwp_cp_p{i}_locx",   ref clocx);
                    dataStore.SyncData($"gwp_cp_p{i}_locy",   ref clocy);
                    dataStore.SyncData($"gwp_cp_p{i}_victim", ref cvict);

                    MobileParty? offender = cid == PlayerCrimeId
                        ? MobileParty.MainParty
                        : MobileParty.All.FirstOrDefault(p => p.StringId == coffe);
                    if (offender == null || !offender.IsActive) continue;

                    _pool.Add(new CrimeRecord
                    {
                        CrimeId      = cid,
                        CrimeType    = ctype,
                        Offender     = offender,
                        OccurredTime = CampaignTime.Hours(ctime),
                        Location     = new Vec2(clocx, clocy),
                        VictimName   = cvict
                    });
                }

                // ── 恢复 _tasks ────────────────────────────────────────────────────
                int taskCount = 0;
                dataStore.SyncData("gwp_cp_task_count", ref taskCount);
                for (int i = 0; i < taskCount; i++)
                {
                    string tpol = ""; string tcid = ""; string tctype = ""; string tcoffe = "";
                    float tctime = 0f; float tclocx = 0f; float tclocy = 0f; string tcvict = "";
                    int twar = 0; string twt = ""; int tesc = 0; string tescs = ""; int tbe = 0;
                    dataStore.SyncData($"gwp_cp_t{i}_police",  ref tpol);
                    dataStore.SyncData($"gwp_cp_t{i}_cid",     ref tcid);
                    dataStore.SyncData($"gwp_cp_t{i}_ctype",   ref tctype);
                    dataStore.SyncData($"gwp_cp_t{i}_coffen",  ref tcoffe);
                    dataStore.SyncData($"gwp_cp_t{i}_ctime",   ref tctime);
                    dataStore.SyncData($"gwp_cp_t{i}_clocx",   ref tclocx);
                    dataStore.SyncData($"gwp_cp_t{i}_clocy",   ref tclocy);
                    dataStore.SyncData($"gwp_cp_t{i}_cvictim", ref tcvict);
                    dataStore.SyncData($"gwp_cp_t{i}_war",     ref twar);
                    dataStore.SyncData($"gwp_cp_t{i}_wt",      ref twt);
                    dataStore.SyncData($"gwp_cp_t{i}_esc",     ref tesc);
                    dataStore.SyncData($"gwp_cp_t{i}_escs",    ref tescs);
                    dataStore.SyncData($"gwp_cp_t{i}_be",      ref tbe);

                    if (string.IsNullOrEmpty(tpol) || string.IsNullOrEmpty(tcid)) continue;

                    // 验证警察部队仍然存在
                    MobileParty? policeParty = MobileParty.All.FirstOrDefault(p => p.StringId == tpol);
                    if (policeParty == null) continue;

                    // 验证目标罪犯仍然存在
                    MobileParty? offender = tcid == PlayerCrimeId
                        ? MobileParty.MainParty
                        : MobileParty.All.FirstOrDefault(p => p.StringId == tcoffe);
                    if (offender == null || !offender.IsActive) continue;

                    var crime = new CrimeRecord
                    {
                        CrimeId      = tcid,
                        CrimeType    = tctype,
                        Offender     = offender,
                        OccurredTime = CampaignTime.Hours(tctime),
                        Location     = new Vec2(tclocx, tclocy),
                        VictimName   = tcvict
                    };

                    // 恢复 WarTarget（Kingdom 或 Clan）
                    IFaction? warTarget = null;
                    if (!string.IsNullOrEmpty(twt))
                        warTarget = (IFaction)Kingdom.All.FirstOrDefault(k => k.StringId == twt)
                                 ?? Clan.All.FirstOrDefault(c => c.StringId == twt);

                    // 恢复 EscortSettlement
                    Settlement? escortSett = null;
                    if (!string.IsNullOrEmpty(tescs))
                        escortSett = Settlement.FindFirst(s => s.StringId == tescs);

                    _tasks[tpol] = new PoliceTask
                    {
                        PolicePartyId       = tpol,
                        TargetCrime         = crime,
                        WarDeclared         = twar != 0,
                        WarTarget           = warTarget,
                        IsEscortingPlayer   = tesc != 0,
                        EscortSettlement    = escortSett,
                        IsPlayerBountyEscort = tbe != 0
                    };
                }

                RefreshAccepting();
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
                $"灰袍守卫已记录你的罪行：{type}（{detail}）| {GetReputationDisplay()}", Colors.Red));

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
                $"灰袍守卫注意到你的善行：{type}（{detail}）| {GetReputationDisplay()}", Colors.Green));
        }

        public static string GetReputationDisplay() => $"声望：{Reputation}";

        public static void ResetReputation(int value) => Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, value));
        public static void ChangeReputation(int delta) => Reputation = Math.Max(MinReputation, Math.Min(MaxReputation, Reputation + delta));
        public static void SetAtonementTaskActive(bool active) => HasAtonementTask = active;

        public static void ClearAll()
        {
            Reputation = 0;
            HasAtonementTask = false;
            _records.Clear();
            _victimFactions.Clear();
        }
    }
}
