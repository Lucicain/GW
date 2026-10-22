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
    internal enum GwpCitySearchPlace { None, Entry, Hall, Arena, Escape }

    internal sealed partial class GwpCitySearchBehavior
    {
        private string _searchSettlementId = string.Empty;
        private string _searchTargetId = string.Empty;
        private int _searchPlace;
        private bool _openPending;
        private bool _searchWar;
        private bool _captureReady;
        private bool _leavePending;
        private List<string> _searchTroops = new List<string>();
        private List<double> _searchHealth = new List<double>();
        // 场景内的对象引用不存；地图衔接从已存的 ID 解析，不能把 Location/Agent 放进存档。
        private Hero? _searchTarget;
        internal GwpCitySearchPlace SearchPlace => (GwpCitySearchPlace)_searchPlace;
        internal bool SearchActive => SearchPlace != GwpCitySearchPlace.None;
        internal bool SearchWar => _searchWar;
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
            _searchPlace = (int)place;
            _openPending = true;
        }

        internal void MarkSearchWar() => _searchWar = true;

        internal void WonHall()
        {
            _captureReady = !SearchTarget.IsDead;
            QueuePlace(GwpCitySearchPlace.Escape);
        }

        internal void FinishSearch(bool escaped)
        {
            Hero? target = SearchTarget;
            // 群殴胜利先控制住人，杀出城后才正式交进主队囚笼；途中倒下不能隔城领到俘虏。
            bool captured = escaped && _captureReady && target != null && !target.IsDead && !target.IsPrisoner
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
            FinishSearch(true);
        }

        internal static void Pump()
        {
            if (Campaign.Current == null || Game.Current?.GameType is not Campaign) return;
            var behavior = Campaign.Current.GetCampaignBehavior<GwpCitySearchBehavior>();
            if (behavior == null || (!behavior._openPending && !behavior._leavePending) || Mission.Current != null
                || Campaign.Current.ConversationManager.IsConversationInProgress
                || GameStateManager.Current.ActiveState is not MapState) return;
            if (behavior._leavePending)
            {
                behavior._leavePending = false;
                if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.LeaveSettlement();
                    PlayerEncounter.Finish();
                }
            }
            else behavior.OpenPendingSearch();
        }

        private void OpenPendingSearch()
        {
            Settlement? settlement = Settlement.CurrentSettlement;
            Hero target = SearchTarget;
            if (settlement?.StringId != _searchSettlementId || Hero.MainHero.IsPrisoner
                || SearchPlace == GwpCitySearchPlace.Entry && Hero.MainHero.IsWounded
                || target == null || target.IsPrisoner
                || (SearchPlace != GwpCitySearchPlace.Escape && (target.IsDead || CrimeRecord.ShelterOf(target) != settlement)))
            {
                FinishSearch(false);
                return;
            }
            _openPending = false;
            // 原版 Passage 会写 NextLocation 并自动开普通步行场景；自有衔接清掉它，避免抢先打开而丢兵。
            Campaign.Current.GameMenuManager.NextLocation = null;
            // 回中心从大厅门出来：原版 MissionLocationLogic 用 PreviousLocation 找真实的门框，
            // 不依赖城镇守卫出生标签（城堡不保证有这个标签）。
            Campaign.Current.GameMenuManager.PreviousLocation = SearchPlace == GwpCitySearchPlace.Escape
                ? settlement.LocationComplex.GetLocationWithId("lordshall") : null;
            if (SearchPlace == GwpCitySearchPlace.Arena)
            {
                var arena = settlement.LocationComplex.GetLocationWithId("arena");
                CampaignMission.OpenArenaDuelMission(arena.GetSceneName(settlement.Town.GetWallLevel()), arena,
                    target.CharacterObject, false, false, winner =>
                    {
                        if (winner == CharacterObject.PlayerCharacter) _captureReady = true;
                        QueuePlace(GwpCitySearchPlace.Escape);
                    }, 100f);
                Mission.Current.AddMissionBehavior(new GwpCitySearchArenaReturn(this));
                return;
            }
            TroopRoster roster = TroopRoster.CreateDummyTroopRoster();
            foreach (string id in _searchTroops)
                roster.AddToCounts(TaleWorlds.ObjectSystem.MBObjectManager.Instance.GetObject<CharacterObject>(id), 1);
            GwpCitySearchMission.Open(settlement, target, roster);
        }
    }

    /// <summary>原版竞技场裁定胜负；只在结果已确定、回到任务帧之后退出，不在倒地回调里切场景。</summary>
    internal sealed class GwpCitySearchArenaReturn : MissionLogic
    {
        private readonly GwpCitySearchBehavior _search;
        internal GwpCitySearchArenaReturn(GwpCitySearchBehavior search) => _search = search;
        public override void OnMissionTick(float dt)
        {
            if (_search.SearchPlace != GwpCitySearchPlace.Arena) Mission.EndMission();
        }
        protected override void OnEndMission()
        {
            if (_search.SearchPlace == GwpCitySearchPlace.Arena) _search.FinishSearch(false);
        }
    }
}
