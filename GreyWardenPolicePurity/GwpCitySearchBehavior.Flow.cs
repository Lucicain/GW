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
        // 场景内的对象引用不存；地图衔接从已存的 ID 解析，不能把 Location/Agent 放进存档。
        private Hero? _searchTarget;
        internal GwpCitySearchPlace SearchPlace => (GwpCitySearchPlace)_searchPlace;
        internal bool SearchActive => SearchPlace != GwpCitySearchPlace.None;
        internal bool SearchWar => _searchWar;
        internal bool SearchHere => SearchActive && Settlement.CurrentSettlement?.StringId == _searchSettlementId;
        internal GwpCitySearchGate GateState { get => (GwpCitySearchGate)_gateState; set => _gateState = (int)value; }
        internal bool HallAccessible => GateState == GwpCitySearchGate.Allowed || GateState == GwpCitySearchGate.Cleared;
        internal bool DuelAgreed => _duelAgreed;
        internal bool Returning => _returning;
        internal bool HallResolved => _duelAgreed || _returning;
        internal bool EscapeCleared { get => _escapeCleared; set => _escapeCleared = value; }
        internal Hero SearchTarget => _searchTarget ??= Hero.FindFirst(hero => hero.StringId == _searchTargetId);

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
        }

        private void BeginSearch(Settlement settlement, Hero target, TroopRoster roster)
        {
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
            foreach (TroopRosterElement element in roster.GetTroopRoster())
                if (!element.Character.IsPlayerCharacter)
                    for (int i = 0; i < element.Number - element.WoundedNumber; i++)
                    {
                        _searchTroops.Add(element.Character.StringId);
                        _searchHealth.Add(-1d); // 首次生成用原版满血；后续保留每个存活者的实际血量。
                    }
            QueuePlace(GwpCitySearchPlace.Entry);
        }

        internal void SaveSurvivors(IEnumerable<(CharacterObject Character, float Health)> survivors)
        {
            _searchTroops.Clear();
            _searchHealth.Clear();
            foreach (var survivor in survivors)
            {
                _searchTroops.Add(survivor.Character.StringId);
                _searchHealth.Add(survivor.Health);
            }
        }

        internal float SavedHealth(string troopId, int ordinal)
        {
            for (int i = 0; i < _searchTroops.Count; i++)
                if (_searchTroops[i] == troopId && ordinal-- == 0) return (float)_searchHealth[i];
            throw new InvalidOperationException("Search survivor missing: " + troopId);
        }

        internal void QueuePlace(GwpCitySearchPlace place)
        {
            if (place != SearchPlace)
                _previousLocationId = SearchPlace == GwpCitySearchPlace.None ? string.Empty
                    : SearchPlace == GwpCitySearchPlace.Hall ? "lordshall"
                    : SearchPlace == GwpCitySearchPlace.Arena ? "arena" : "center";
            _searchPlace = (int)place;
            _openPending = true;
        }

        internal void PauseSearch() => _openPending = false;

        internal void MarkSearchWar() => _searchWar = true;

        internal void WonHall()
        {
            _captureReady = !SearchTarget.IsDead;
            _returning = true;
            _duelAgreed = false;
        }

        internal void AgreeDuel() => _duelAgreed = true;
        internal void WithdrawFromHall() => _returning = true;

        internal void FinishSearch(bool escaped)
        {
            Hero? target = SearchTarget;
            // 群殴胜利先控制住人，杀出城后才正式交进主队囚笼；途中倒下不能隔城领到俘虏。
            bool captured = escaped && _captureReady && target != null && !target.IsDead && !target.IsPrisoner
                && Settlement.CurrentSettlement?.StringId == _searchSettlementId
                && CrimeRecord.ShelterOf(target) == Settlement.CurrentSettlement
                && GwpVoluntarySurrender.TryCapture(PartyBase.MainParty, target);
            if (captured) Bounty?.RegisterCaptureInCustody();
#if GWP_DIAGNOSTICS
            GwpAiDiagnostics.WriteCitySearch("SEARCH_COMPLETE", "escaped=" + escaped + "; captured=" + captured
                + "; target=" + target?.StringId + "; place=" + SearchPlace + "; war=" + _searchWar);
#endif
            _searchPlace = (int)GwpCitySearchPlace.None;
            _openPending = false;
            _captureReady = false;
            _searchTarget = null;
            _searchTroops.Clear();
            _searchHealth.Clear();
            _leavePending = escaped;
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
            if (behavior.SearchActive && !behavior.SearchHere) behavior.FinishSearch(false);
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
            _openPending = false;
            // 原版 Passage 会写 NextLocation 并自动开普通步行场景；自有衔接清掉它，避免抢先打开而丢兵。
            Campaign.Current.GameMenuManager.NextLocation = null;
            // 与原版门衔接同源：记录从哪个地点来，中心出生在对应门口；Tab 只暂停，不推进。
            Campaign.Current.GameMenuManager.PreviousLocation = string.IsNullOrEmpty(_previousLocationId)
                ? null : settlement.LocationComplex.GetLocationWithId(_previousLocationId);
            if (SearchPlace == GwpCitySearchPlace.Arena)
            {
                var arena = settlement.LocationComplex.GetLocationWithId("arena");
                // 原版单挑从sp_arena摆双方；菜单直达不代表大厅与竞技场之间存在门。
                Campaign.Current.GameMenuManager.PreviousLocation = null;
#if GWP_DIAGNOSTICS
                GwpAiDiagnostics.WriteCitySearch("ARENA_OPEN", "settlement=" + settlement.StringId
                    + "; target=" + target.StringId + "; scene=" + arena.GetSceneName(settlement.Town.GetWallLevel())
                    + "; from=" + _previousLocationId);
#endif
                var mission = (Mission)CampaignMission.OpenArenaDuelMission(arena.GetSceneName(settlement.Town.GetWallLevel()), arena,
                    target.CharacterObject, false, false, winner =>
                    {
                        if (winner == CharacterObject.PlayerCharacter) WonHall();
                        else FinishSearch(false);
                    }, 100f);
                // OpenNew返回新任务时尚未Initialize，Mission.Current此时仍为空；绑定到返回的实例。
                mission.AddMissionBehavior(new GwpCitySearchArenaReturn(this));
                return;
            }
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            // 暂停后玩家可以在聚落菜单调走部下；保存的现场名单不能凭空再生成已不在主队的人。
            var available = PartyBase.MainParty.MemberRoster.GetTroopRoster()
                .ToDictionary(element => element.Character, element => element.Number - element.WoundedNumber);
            var survivors = new List<(CharacterObject Character, float Health)>();
            for (int i = 0; i < _searchTroops.Count; i++)
            {
                CharacterObject character = TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>(_searchTroops[i]);
                if (!available.TryGetValue(character, out int count) || count <= 0) continue;
                available[character] = count - 1;
                roster.AddToCounts(character, 1);
                survivors.Add((character, (float)_searchHealth[i]));
            }
            SaveSurvivors(survivors);
            GwpCitySearchMission.Open(settlement, target, roster);
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
