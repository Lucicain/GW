using System;
using System.Collections.Generic;
using System.Linq;
using SandBox;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GreyWardenPolicePurity
{
    internal enum GwpCitySearchPlace { None, Entry, Hall, Arena, Escape, Street }
    internal enum GwpCitySearchGate { Unchecked, Allowed, Leaving, Refused, Withdrawing, Cleared }

    internal sealed partial class GwpCitySearchBehavior
    {
        private string _searchSettlementId = string.Empty;
        private string _searchTargetId = string.Empty;
        private int _searchPlace;
        private bool _openPending;
        private bool _searchWar;
        private bool _captureReady;
        private bool _leavePending;
        private int _gateState;
        private bool _duelAgreed;
        private bool _returning;
        private bool _escapeCleared;
        private string _previousLocationId = string.Empty;
        private List<string> _searchTroops = new List<string>();
        private List<double> _searchHealth = new List<double>();
        // 与 _searchTroops 一一对应：1 是灰袍办案队的人，0 是玩家本队的人；伤亡写回各自名册。
        private List<int> _searchTroopSupport = new List<int>();
        private List<string> _enemyKeys = new List<string>();
        private List<double> _enemyHealth = new List<double>();
        private List<string> _enemyLosses = new List<string>();
        private bool _entryWaveQueued;
        private bool _hasLeftHall;
        private bool _targetDown;
        private bool _targetKilled;
        // 场景内的对象引用不存；地图衔接从已存的 ID 解析，不能把 Location/Agent 放进存档。
        private Hero? _searchTarget;
        internal GwpCitySearchPlace SearchPlace => (GwpCitySearchPlace)_searchPlace;
        internal bool SearchActive => SearchPlace != GwpCitySearchPlace.None;
        internal bool SearchWar => _searchWar;
        internal bool HasLeftHall => _hasLeftHall;
        internal bool SearchHere => SearchActive && Settlement.CurrentSettlement?.StringId == _searchSettlementId;
        internal GwpCitySearchGate GateState { get => (GwpCitySearchGate)_gateState; set => _gateState = (int)value; }
        internal bool HallAccessible => GateState == GwpCitySearchGate.Allowed || GateState == GwpCitySearchGate.Cleared;
        internal bool DuelAgreed => _duelAgreed;
        internal bool Returning => _returning;
        internal bool TargetResolved => _captureReady || _targetDown;
        internal bool HallResolved => _duelAgreed || _returning;
        internal bool EscapeCleared { get => _escapeCleared; set => _escapeCleared = value; }
        internal Hero SearchTarget => _searchTarget ??= Hero.FindFirst(hero => hero.StringId == _searchTargetId);
        internal bool UsesHallDuel
        {
            get
            {
                Settlement? settlement = Settlement.CurrentSettlement;
                return string.IsNullOrEmpty(settlement?.LocationComplex.GetLocationWithId("arena")
                    ?.GetSceneName(settlement.Town.GetWallLevel()));
            }
        }
        internal bool HallDuelLocked => SearchActive && DuelAgreed && UsesHallDuel && !SearchWar && !Returning;

        private void SyncSearch(IDataStore store)
        {
            store.SyncData("gwp_cs_settlement", ref _searchSettlementId);
            store.SyncData("gwp_cs_target", ref _searchTargetId);
            store.SyncData("gwp_cs_place", ref _searchPlace);
            store.SyncData("gwp_cs_pending", ref _openPending);
            store.SyncData("gwp_cs_war", ref _searchWar);
            store.SyncData("gwp_cs_capture", ref _captureReady);
            store.SyncData("gwp_cs_leave", ref _leavePending);
            store.SyncData("gwp_cs_gate", ref _gateState);
            store.SyncData("gwp_cs_duel", ref _duelAgreed);
            store.SyncData("gwp_cs_returning", ref _returning);
            store.SyncData("gwp_cs_escape_cleared", ref _escapeCleared);
            store.SyncData("gwp_cs_previous", ref _previousLocationId);
            store.SyncData("gwp_cs_troops", ref _searchTroops);
            store.SyncData("gwp_cs_health", ref _searchHealth);
            store.SyncData("gwp_cs_troop_support", ref _searchTroopSupport);
            store.SyncData("gwp_cs_enemy_keys", ref _enemyKeys);
            store.SyncData("gwp_cs_enemy_health", ref _enemyHealth);
            store.SyncData("gwp_cs_enemy_losses", ref _enemyLosses);
            store.SyncData("gwp_cs_entry_wave", ref _entryWaveQueued);
            store.SyncData("gwp_cs_left_hall", ref _hasLeftHall);
            store.SyncData("gwp_cs_target_down", ref _targetDown);
            store.SyncData("gwp_cs_target_killed", ref _targetKilled);
            store.SyncData("gwp_cs_source_party", ref _searchSourcePartyId);
            store.SyncData("gwp_cs_attempt_started", ref _searchAttemptStarted);
            SyncStreetBodies(store);
        }

        private void BeginSearch(Settlement settlement, Hero target, TroopRoster roster)
        {
            if (!PrepareSearchSource(target)) return;
            _searchSettlementId = settlement.StringId;
            _searchTargetId = target.StringId;
            _searchTarget = target;
            _searchWar = false;
            _captureReady = false;
            _leavePending = false;
            GateState = GwpCitySearchGate.Unchecked;
            _duelAgreed = _returning = _escapeCleared = false;
            _previousLocationId = string.Empty;
            _searchTroops.Clear();
            _searchHealth.Clear();
            _searchTroopSupport.Clear();
            _enemyKeys.Clear();
            _enemyHealth.Clear();
            _enemyLosses.Clear();
            ClearStreetBodies();
            _entryWaveQueued = _hasLeftHall = _targetDown = _targetKilled = false;
            // 选兵结果里含锁定的办案队全员（GwpCitySearchSupportSelection），按办案队名册拆出来，余下是本队的人。
            var supportCounts = new Dictionary<CharacterObject, int>();
            foreach (TroopRosterElement element in SupportSource?.MemberRoster.GetTroopRoster() ?? Enumerable.Empty<TroopRosterElement>())
                if (!element.Character.IsHero)
                    supportCounts[element.Character] = element.Number - element.WoundedNumber;
            foreach (TroopRosterElement element in roster.GetTroopRoster())
            {
                if (element.Character.IsPlayerCharacter) continue;
                supportCounts.TryGetValue(element.Character, out int support);
                for (int i = 0; i < element.Number - element.WoundedNumber; i++)
                {
                    _searchTroops.Add(element.Character.StringId);
                    _searchHealth.Add(-1d); // 首次生成用原版满血；后续保留每个存活者的实际血量。
                    _searchTroopSupport.Add(i < support ? 1 : 0);
                }
            }
            QueuePlace(GwpCitySearchPlace.Entry);
        }

        internal void SaveSurvivors(IEnumerable<(CharacterObject Character, float Health, bool Support)> survivors)
        {
            _searchTroops.Clear();
            _searchHealth.Clear();
            _searchTroopSupport.Clear();
            foreach (var survivor in survivors)
            {
                _searchTroops.Add(survivor.Character.StringId);
                _searchHealth.Add(survivor.Health);
                _searchTroopSupport.Add(survivor.Support ? 1 : 0);
            }
        }

        internal float SavedHealth(string troopId, int ordinal, bool support)
        {
            for (int i = 0; i < _searchTroops.Count; i++)
                if (_searchTroops[i] == troopId && (_searchTroopSupport[i] != 0) == support && ordinal-- == 0)
                    return (float)_searchHealth[i];
            throw new InvalidOperationException("Search survivor missing: " + troopId);
        }

        internal float SavedEnemyHealth(string key, int ordinal)
        {
            for (int i = 0; i < _enemyKeys.Count; i++)
                if (_enemyKeys[i] == key && ordinal-- == 0) return (float)_enemyHealth[i];
            return -1f;
        }

        internal void SaveEnemySurvivors(IEnumerable<string> observedKeys, IEnumerable<(string Key, float Health)> survivors)
        {
            var replaced = observedKeys.GroupBy(key => key).ToDictionary(group => group.Key, group => group.Count());
            for (int i = 0; i < _enemyKeys.Count;)
                if (replaced.TryGetValue(_enemyKeys[i], out int count) && count > 0)
                {
                    replaced[_enemyKeys[i]] = count - 1;
                    _enemyKeys.RemoveAt(i);
                    _enemyHealth.RemoveAt(i);
                }
                else i++;
            // 本场只接触同兵种的前N名；其余街道个体不能被大厅的小快照清掉。
            var current = survivors.ToArray();
            _enemyKeys.InsertRange(0, current.Select(survivor => survivor.Key));
            _enemyHealth.InsertRange(0, current.Select(survivor => (double)survivor.Health));
        }

        internal void QueuePlace(GwpCitySearchPlace place)
        {
            if (HallDuelLocked && place != GwpCitySearchPlace.None) place = GwpCitySearchPlace.Hall;
            if (SearchPlace == GwpCitySearchPlace.Hall && (place == GwpCitySearchPlace.Street || place == GwpCitySearchPlace.Escape))
                _hasLeftHall = true;
            if (place != SearchPlace)
                _previousLocationId = SearchPlace == GwpCitySearchPlace.None ? string.Empty
                    : SearchPlace == GwpCitySearchPlace.Hall ? "lordshall"
                    : SearchPlace == GwpCitySearchPlace.Arena ? "arena" : "center";
            _searchPlace = (int)place;
            _openPending = true;
        }

        internal void PauseSearch() => _openPending = false;

        internal void MarkSearchWar() => _searchWar = true;

        internal int EnemyLossCount(string key) => _enemyLosses.Count(loss => loss == key);
        internal void RecordEnemyLoss(string key) => _enemyLosses.Add(key);
        internal bool ClaimEntryWave()
        {
            if (_entryWaveQueued) return false;
            _entryWaveQueued = true;
            return true;
        }

        internal void MarkTargetDown(bool killed)
        {
            _targetDown = true;
            _targetKilled = killed;
            _captureReady = !killed && !SearchTarget.IsDead;
            _returning = true;
            _duelAgreed = false;
        }

        internal void WonHall()
        {
            _captureReady = !SearchTarget.IsDead;
            _returning = true;
            _duelAgreed = false;
        }

        internal void AgreeDuel() => _duelAgreed = true;
        internal void WithdrawFromHall() => _returning = true;

        internal void LostSearchDuel() => FinishSearch(false, duelLost: true);

        internal void FinishSearch(bool escaped, bool duelLost = false, Settlement? leftSettlement = null)
        {
            Hero? target = SearchTarget;
            Settlement? settlement = leftSettlement ?? Settlement.CurrentSettlement;
            if (Supported) Enforcement?.ReleaseCitySearchParty(_searchSourcePartyId);
            // 群殴胜利先控制住人，杀出城后才正式交进主队囚笼；途中倒下不能隔城领到俘虏。
            bool captured = escaped && _captureReady && target != null && !target.IsDead && !target.IsPrisoner
                && settlement?.StringId == _searchSettlementId
                && CrimeRecord.ShelterOf(target) == settlement
                && GwpVoluntarySurrender.TryCapture(PartyBase.MainParty, target);
            if (captured) Bounty?.RegisterCaptureInCustody();
            if (escaped && _targetDown && _targetKilled && target != null) Bounty?.RecordCitySearchDefeat(target);
            if (Supported && _searchAttemptStarted && !captured && target != null
                && !(escaped && _targetDown && _targetKilled))
            {
                if (duelLost) Bounty?.RecordCitySearchDuelAgreement(target);
                else Bounty?.FailCitySearchCommission(target);
            }
            _searchPlace = (int)GwpCitySearchPlace.None;
            _openPending = false;
            _captureReady = false;
            _searchTarget = null;
            _searchTroops.Clear();
            _searchHealth.Clear();
            _searchTroopSupport.Clear();
            _enemyKeys.Clear();
            _enemyHealth.Clear();
            _enemyLosses.Clear();
            ClearStreetBodies();
            _targetDown = _targetKilled = false;
            _searchSourcePartyId = string.Empty;
            _searchAttemptStarted = false;
            _leavePending = escaped && leftSettlement == null;
        }

        internal void PeacefulCapture()
        {
            _captureReady = true;
            _returning = true;
        }

        internal static void Pump()
        {
            if (Campaign.Current == null || Game.Current?.GameType is not Campaign) return;
            var behavior = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
            if (behavior == null || Mission.Current != null
                || Campaign.Current.ConversationManager.IsConversationInProgress
                || GameStateManager.Current.ActiveState is not MapState) return;
            if (behavior.SearchActive && !behavior.SearchHere)
            {
                behavior.FinishSearch(false);
            }
            if (behavior._leavePending)
            {
                behavior._leavePending = false;
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish();
                }
            }
            else if (behavior._openPending) behavior.OpenPendingSearch();
        }

        private void OpenPendingSearch()
        {
            Settlement? settlement = Settlement.CurrentSettlement;
            Hero target = SearchTarget;
            if (settlement?.StringId != _searchSettlementId || Hero.MainHero.IsPrisoner
                || target == null || target.IsPrisoner
                || (!_returning && (target.IsDead || CrimeRecord.ShelterOf(target) != settlement)))
            {
                FinishSearch(false);
                return;
            }
            if (Supported && SupportSource == null || !StartSearchAttempt())
            {
                FinishSearch(false);
                return;
            }
            if (HallDuelLocked)
            {
                if (SearchPlace != GwpCitySearchPlace.Hall) QueuePlace(GwpCitySearchPlace.Hall);
                Location? from = settlement.LocationComplex.GetLocationOfCharacter(target);
                if (from?.StringId != "lordshall")
                    settlement.LocationComplex.ChangeLocation(settlement.LocationComplex.GetLocationCharacterOfHero(target),
                        from, settlement.LocationComplex.GetLocationWithId("lordshall"));
            }
            _openPending = false;
            // 原版 Passage 会写 NextLocation 并自动开普通步行场景；自有衔接清掉它，避免抢先打开而丢兵。
            if (DuelAgreed && SearchPlace == GwpCitySearchPlace.Street)
            {
                Location? from = settlement.LocationComplex.GetLocationOfCharacter(target);
                if (from?.StringId == "lordshall")
                    settlement.LocationComplex.ChangeLocation(settlement.LocationComplex.GetLocationCharacterOfHero(target),
                        from, settlement.LocationComplex.GetLocationWithId("center"));
            }
            Campaign.Current.GameMenuManager.NextLocation = null;
            // 与原版门衔接同源：记录从哪个地点来，中心出生在对应门口；Tab 只暂停，不推进。
            Campaign.Current.GameMenuManager.PreviousLocation = string.IsNullOrEmpty(_previousLocationId)
                ? null : settlement.LocationComplex.GetLocationWithId(_previousLocationId);
            if (SearchPlace == GwpCitySearchPlace.Arena)
            {
                var arena = settlement.LocationComplex.GetLocationWithId("arena");
                // 原版单挑从sp_arena摆双方；菜单直达不代表大厅与竞技场之间存在门。
                Campaign.Current.GameMenuManager.PreviousLocation = null;
                var mission = (Mission)CampaignMission.OpenArenaDuelMission(arena.GetSceneName(settlement.Town.GetWallLevel()), arena,
                    target.CharacterObject, false, false, winner =>
                    {
                        if (winner == CharacterObject.PlayerCharacter) WonHall();
                        else LostSearchDuel();
                    }, 100f);
                // OpenNew返回新任务时尚未Initialize，Mission.Current此时仍为空；绑定到返回的实例。
                mission.AddMissionBehavior(new GwpCitySearchArenaReturn(this));
                return;
            }
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            TroopRoster supportRoster = TroopRoster.CreateDummyTroopRoster();
            // 暂停后玩家可以在聚落菜单调走部下；保存的现场名单不能凭空再生成已不在各自名册的人。
            var available = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .ToDictionary(element => element.Character, element => element.Number - element.WoundedNumber);
            var supportAvailable = (SupportSource?.MemberRoster.GetTroopRoster() ?? Enumerable.Empty<TroopRosterElement>())
                .ToDictionary(element => element.Character, element => element.Number - element.WoundedNumber);
            var survivors = new List<(CharacterObject Character, float Health, bool Support)>();
            for (int i = 0; i < _searchTroops.Count; i++)
            {
                CharacterObject character = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>(_searchTroops[i]);
                bool support = _searchTroopSupport[i] != 0;
                var pool = support ? supportAvailable : available;
                if (!pool.TryGetValue(character, out int count) || count <= 0) continue;
                pool[character] = count - 1;
                (support ? supportRoster : roster).AddToCounts(character, 1);
                survivors.Add((character, (float)_searchHealth[i], support));
            }
            SaveSurvivors(survivors);
            GwpCitySearchMission.Open(settlement, target, roster, supportRoster);
        }
    }

    /// <summary>原版竞技场结束后由玩家自己 Tab；不抢在胜负回调里自动离场。</summary>
    internal sealed class GwpCitySearchArenaReturn : MissionLogic
    {
        private readonly GwpCitySearchBehavior _search;
        internal GwpCitySearchArenaReturn(GwpCitySearchBehavior search) => _search = search;
        protected override void OnEndMission()
        {
            if (!_search.SearchActive) return;
            if (_search.Returning)
            {
                _search.QueuePlace(GwpCitySearchPlace.Escape);
                _search.PauseSearch();
            }
            else _search.FinishSearch(false);
        }
    }
}
