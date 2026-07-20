using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GreyWardenPolicePurity
{
    /// <summary>
    /// Scrollable case-ledger overlay opened from the Grey Warden clan encyclopedia page.
    /// It reads the current open-case pool directly, so it is also useful for live campaign testing.
    /// </summary>
    public sealed class GwpCaseArchiveScreen
    {
        private static GwpCaseArchiveScreen? _activeOverlay;

        private readonly ScreenBase _hostScreen;
        private readonly GwpCaseArchiveVM _dataSource;
        private GauntletLayer? _gauntletLayer;

        private GwpCaseArchiveScreen(ScreenBase hostScreen)
        {
            _hostScreen = hostScreen;
            _dataSource = new GwpCaseArchiveVM(Close);
        }

        public static void Show()
        {
            ScreenBase? hostScreen = ScreenManager.TopScreen;
            if (hostScreen == null)
                return;

            CloseActive();
            var overlay = new GwpCaseArchiveScreen(hostScreen);
            _activeOverlay = overlay;
            overlay.Open();
        }

        public static void CloseActive()
        {
            _activeOverlay?.Close();
        }

        private void Open()
        {
            _gauntletLayer = new GauntletLayer("GwpCaseArchiveOverlay", 230, false)
            {
                IsFocusLayer = true,
                ActiveCursor = CursorType.Default
            };

            _gauntletLayer.InputRestrictions.SetInputRestrictions(
                true,
                InputUsageMask.All | InputUsageMask.BlockEverythingWithoutHitTest);

            _hostScreen.AddLayer(_gauntletLayer);
            _gauntletLayer.LoadMovie("GwpCaseArchive", _dataSource);
            ScreenManager.TrySetFocus(_gauntletLayer);
        }

        private void Close()
        {
            if (_gauntletLayer != null)
            {
                ScreenManager.TryLoseFocus(_gauntletLayer);
                _gauntletLayer.InputRestrictions.ResetInputRestrictions();

                if (_hostScreen.HasLayer(_gauntletLayer))
                    _hostScreen.RemoveLayer(_gauntletLayer);

                _gauntletLayer = null;
            }

            if (ReferenceEquals(_activeOverlay, this))
                _activeOverlay = null;
        }
    }

    public sealed class GwpCaseArchiveVM : ViewModel
    {
        private readonly Action _onClose;
        private string _title = string.Empty;
        private string _summary = string.Empty;
        private string _emptyText = string.Empty;
        private string _refreshText = string.Empty;
        private string _closeText = string.Empty;
        private bool _isEmpty;

        public GwpCaseArchiveVM(Action onClose)
        {
            _onClose = onClose;
            Cases = new MBBindingList<GwpCaseArchiveItemVM>();
            Title = GwpText.Get("{=gwp_gwpcasearchivescreen_001}Grey Warden case ledger");
            EmptyText = GwpText.Get("{=gwp_gwpcasearchivescreen_002}There are currently no tasks in the pool.");
            RefreshText = GwpText.Get("{=gwp_gwpcasearchivescreen_003}Refresh ledger");
            CloseText = GwpText.Get("{=gwp_gwpcasearchivescreen_004}Close");
            RefreshLedger();
        }

        [DataSourceProperty]
        public MBBindingList<GwpCaseArchiveItemVM> Cases { get; }

        [DataSourceProperty]
        public string Title
        {
            get => _title;
            set
            {
                if (value == _title) return;
                _title = value;
                OnPropertyChangedWithValue(value, nameof(Title));
            }
        }

        [DataSourceProperty]
        public string Summary
        {
            get => _summary;
            set
            {
                if (value == _summary) return;
                _summary = value;
                OnPropertyChangedWithValue(value, nameof(Summary));
            }
        }

        [DataSourceProperty]
        public string EmptyText
        {
            get => _emptyText;
            set
            {
                if (value == _emptyText) return;
                _emptyText = value;
                OnPropertyChangedWithValue(value, nameof(EmptyText));
            }
        }

        [DataSourceProperty]
        public string RefreshText
        {
            get => _refreshText;
            set
            {
                if (value == _refreshText) return;
                _refreshText = value;
                OnPropertyChangedWithValue(value, nameof(RefreshText));
            }
        }

        [DataSourceProperty]
        public string CloseText
        {
            get => _closeText;
            set
            {
                if (value == _closeText) return;
                _closeText = value;
                OnPropertyChangedWithValue(value, nameof(CloseText));
            }
        }

        [DataSourceProperty]
        public bool IsEmpty
        {
            get => _isEmpty;
            set
            {
                if (value == _isEmpty) return;
                _isEmpty = value;
                OnPropertyChangedWithValue(value, nameof(IsEmpty));
            }
        }

        public void ExecuteRefresh()
        {
            RefreshLedger();
        }

        public void ExecuteClose()
        {
            _onClose();
        }

        private void RefreshLedger()
        {
            Cases.Clear();

            var assignedCases = CrimePool.ActiveTasks.Values
                .Select(static task => new { Task = task, Record = task.TargetCrime })
                .Where(static row => row.Record?.HasOpenCase == true)
                .GroupBy(static row => row.Record!.CrimeId, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .OrderByDescending(static row => row.Record!.LastCrimeTime.ToHours)
                .ThenBy(static row => row.Record!.CrimeId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var assignedCrimeIds = new HashSet<string>(
                assignedCases.Select(row => row.Record!.CrimeId),
                StringComparer.OrdinalIgnoreCase);
            var unassignedCases = CrimePool.LedgerRecords
                .Where(record => record.HasOpenCase && !assignedCrimeIds.Contains(record.CrimeId))
                .OrderByDescending(record => record.LastCrimeTime.ToHours)
                .ThenBy(record => record.CrimeId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var assistanceTasks = PoliceEnforcementBehavior.GetAssistanceTaskSnapshots()
                .OrderByDescending(task => task.AssignedTime.ToHours)
                .ThenBy(task => task.HelperPartyId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var reliefTasks = GreyWardenVillageAdoptionBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.QueuedTime.ToHours)
                .ThenBy(task => task.VillageSettlementId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var assignedRows = new List<(double Time, string Key, GwpCaseArchiveItemVM Item)>();
            assignedRows.AddRange(assignedCases.Select(row =>
                (row.Record!.LastCrimeTime.ToHours, row.Record.CrimeId,
                    new GwpCaseArchiveItemVM(row.Record, row.Task))));
            assignedRows.AddRange(assistanceTasks.Select(task =>
                (task.AssignedTime.ToHours, "assist:" + task.HelperPartyId,
                    new GwpCaseArchiveItemVM(task))));
            assignedRows.AddRange(reliefTasks.Where(task => task.IsAssigned).Select(task =>
                (task.QueuedTime.ToHours, "relief:" + task.PolicePartyId,
                    new GwpCaseArchiveItemVM(task))));
            foreach (var row in assignedRows
                         .OrderByDescending(row => row.Time)
                         .ThenBy(row => row.Key, StringComparer.OrdinalIgnoreCase))
                Cases.Add(row.Item);

            foreach (CrimeRecord record in unassignedCases)
                Cases.Add(new GwpCaseArchiveItemVM(record, null));
            foreach (GreyWardenVillageAdoptionBehavior.VillageReliefTaskSnapshot task in
                     reliefTasks.Where(task => !task.IsAssigned))
                Cases.Add(new GwpCaseArchiveItemVM(task));

            Summary = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_005}Task pool: {VAR_1}/100   Assigned: {VAR_2}   Waiting: {VAR_3}",
                "VAR_1", Cases.Count,
                "VAR_2", assignedCases.Count + assistanceTasks.Count +
                    reliefTasks.Count(task => task.IsAssigned),
                "VAR_3", unassignedCases.Count + reliefTasks.Count(task => !task.IsAssigned));
            IsEmpty = Cases.Count == 0;
        }
    }

    public sealed class GwpCaseArchiveItemVM : ViewModel
    {
        private string _header = string.Empty;
        private string _timeText = string.Empty;
        private string _assignmentText = string.Empty;
        private string _detailsText = string.Empty;

        public GwpCaseArchiveItemVM(CrimeRecord record, PoliceTask? task)
        {
            string offenderName = ResolveOffenderName(record);
            string status = GwpText.Get("{=gwp_gwpcasearchivescreen_006}OPEN");
            Header = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_008}{VAR_1}  —  {VAR_2}",
                "VAR_1", offenderName,
                "VAR_2", status);

            TimeText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_009}Last offence: {VAR_1}   Case opened: {VAR_2}",
                "VAR_1", FormatCampaignDate(record.LastCrimeTime),
                "VAR_2", FormatCampaignDate(record.OccurredTime));

            AssignmentText = BuildAssignmentText(record, task);

            string crimeType = string.IsNullOrWhiteSpace(record.CrimeType)
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_010}Unrecorded")
                : GwpText.CrimeType(record.CrimeType);
            string victim = string.IsNullOrWhiteSpace(record.VictimName)
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_011}Unrecorded")
                : record.VictimName;
            DetailsText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_012}Offence: {VAR_1}   Victim: {VAR_2}   Location: {VAR_3}",
                "VAR_1", crimeType,
                "VAR_2", victim,
                "VAR_3", FormatLocation(record.Location));
        }

        internal GwpCaseArchiveItemVM(
            PoliceEnforcementBehavior.AssistanceTaskSnapshot assistance)
        {
            MobileParty? leader = MobileParty.All.FirstOrDefault(party =>
                string.Equals(party.StringId, assistance.LeaderPartyId,
                    StringComparison.OrdinalIgnoreCase));
            MobileParty? helper = MobileParty.All.FirstOrDefault(party =>
                string.Equals(party.StringId, assistance.HelperPartyId,
                    StringComparison.OrdinalIgnoreCase));
            MobileParty? target = MobileParty.All.FirstOrDefault(party =>
                string.Equals(party.StringId, assistance.TargetPartyId,
                    StringComparison.OrdinalIgnoreCase));
            string leaderName = leader?.LeaderHero?.Name?.ToString() ??
                                leader?.Name?.ToString() ?? assistance.LeaderPartyId;
            string helperName = helper?.LeaderHero?.Name?.ToString() ??
                                helper?.Name?.ToString() ?? assistance.HelperPartyId;
            string targetName = target?.LeaderHero?.Name?.ToString() ??
                                target?.Name?.ToString() ?? assistance.TargetPartyId;

            Header = GwpText.Get("{=gwp_gwpcasearchivescreen_032}Assistance — {VAR_1}",
                "VAR_1", targetName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_033}Assigned: {VAR_1}",
                "VAR_1", FormatCampaignDate(assistance.AssignedTime));
            AssignmentText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_034}Assignee: {VAR_1}   Supporting: {VAR_2}",
                "VAR_1", helperName, "VAR_2", leaderName);
            DetailsText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_035}Task type: forced army assistance   Source case: {VAR_1}",
                "VAR_1", assistance.CrimeId);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenVillageAdoptionBehavior.VillageReliefTaskSnapshot relief)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, relief.PolicePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = party?.LeaderHero?.Name?.ToString() ??
                              party?.Name?.ToString() ?? relief.PolicePartyId;

            Header = GwpText.Get("{=gwp_gwpcasearchivescreen_036}Village relief — {VAR_1}",
                "VAR_1", relief.VillageName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(relief.QueuedTime));
            AssignmentText = relief.IsAssigned
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_038}Assignee: {VAR_1}   Stage: {VAR_2}",
                    "VAR_1", assignee, "VAR_2", DescribeReliefStage(relief.Stage))
                : GwpText.Get("{=gwp_gwpcasearchivescreen_039}Assignee: waiting for forced assignment");
            DetailsText = relief.Stage == GreyWardenVillageAdoptionBehavior.ReliefStage.StayingInVillage
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_040}Task type: forced adoption relief   Remaining: {VAR_1} hours",
                    "VAR_1", Math.Ceiling(relief.RemainingHours))
                : GwpText.Get("{=gwp_gwpcasearchivescreen_041}Task type: forced adoption relief");
        }

        [DataSourceProperty]
        public string Header
        {
            get => _header;
            set
            {
                if (value == _header) return;
                _header = value;
                OnPropertyChangedWithValue(value, nameof(Header));
            }
        }

        [DataSourceProperty]
        public string TimeText
        {
            get => _timeText;
            set
            {
                if (value == _timeText) return;
                _timeText = value;
                OnPropertyChangedWithValue(value, nameof(TimeText));
            }
        }

        [DataSourceProperty]
        public string AssignmentText
        {
            get => _assignmentText;
            set
            {
                if (value == _assignmentText) return;
                _assignmentText = value;
                OnPropertyChangedWithValue(value, nameof(AssignmentText));
            }
        }

        [DataSourceProperty]
        public string DetailsText
        {
            get => _detailsText;
            set
            {
                if (value == _detailsText) return;
                _detailsText = value;
                OnPropertyChangedWithValue(value, nameof(DetailsText));
            }
        }

        private static string BuildAssignmentText(CrimeRecord record, PoliceTask? task)
        {
            if (task == null)
                return GwpText.Get("{=gwp_gwpcasearchivescreen_013}Tracking: unassigned");

            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase));
            string partyName = party?.Name?.ToString() ?? task.PolicePartyId;
            string? leaderName = party?.LeaderHero?.Name?.ToString();
            string assignee = string.IsNullOrWhiteSpace(leaderName)
                ? partyName
                : GwpText.Get("{=gwp_gwpcasearchivescreen_015}{VAR_1} ({VAR_2})", "VAR_1", leaderName!, "VAR_2", partyName);

            return GwpText.Get(
                "{=gwp_gwpcasearchivescreen_016}Tracking: {VAR_1}   Stage: {VAR_2}",
                "VAR_1", assignee,
                "VAR_2", DescribeTaskStage(task));
        }

        private static string ResolveOffenderName(CrimeRecord record)
        {
            if (string.Equals(record.CrimeId, CrimePool.PlayerCrimeId, StringComparison.OrdinalIgnoreCase))
                return Hero.MainHero?.Name?.ToString() ?? GwpText.Get("{=gwp_gwpcasearchivescreen_017}Player");

            return record.OffenderHero?.Name?.ToString()
                   ?? record.Offender?.LeaderHero?.Name?.ToString()
                   ?? record.Offender?.Name?.ToString()
                   ?? record.OffenderHeroId
                   ?? record.CrimeId;
        }

        private static string DescribeTaskStage(PoliceTask task)
        {
            if (task.IsPlayerBountyEscort)
                return GwpText.Get("{=gwp_gwpcasearchivescreen_018}bounty escort");
            if (task.IsEscortingPlayer)
                return GwpText.Get("{=gwp_gwpcasearchivescreen_019}escort after arrest");
            if (task.IsPreparingDispatch)
                return GwpText.Get("{=gwp_gwpcasearchivescreen_020}preparing dispatch");
            if (task.WarDeclared)
                return GwpText.Get("{=gwp_gwpcasearchivescreen_021}wartime pursuit");
            return GwpText.Get("{=gwp_gwpcasearchivescreen_022}tracking target");
        }

        private static string DescribeReliefStage(
            GreyWardenVillageAdoptionBehavior.ReliefStage stage) => stage switch
        {
            GreyWardenVillageAdoptionBehavior.ReliefStage.WaitingForAssignment =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_042}waiting for assignment"),
            GreyWardenVillageAdoptionBehavior.ReliefStage.AwaitingResupply =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_043}resupplying"),
            GreyWardenVillageAdoptionBehavior.ReliefStage.TravelingToVillage =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_044}travelling to village"),
            GreyWardenVillageAdoptionBehavior.ReliefStage.StayingInVillage =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_045}relief in progress"),
            _ => GwpText.Get("{=gwp_gwpcasearchivescreen_046}unknown")
        };

        private static string FormatCampaignDate(CampaignTime time)
        {
            string season = time.GetSeasonOfYear switch
            {
                CampaignTime.Seasons.Spring => GwpText.Get("{=gwp_gwpcasearchivescreen_023}Spring"),
                CampaignTime.Seasons.Summer => GwpText.Get("{=gwp_gwpcasearchivescreen_024}Summer"),
                CampaignTime.Seasons.Autumn => GwpText.Get("{=gwp_gwpcasearchivescreen_025}Autumn"),
                CampaignTime.Seasons.Winter => GwpText.Get("{=gwp_gwpcasearchivescreen_026}Winter"),
                _ => GwpText.Get("{=gwp_gwpcasearchivescreen_027}Unknown season")
            };

            return GwpText.Get(
                "{=gwp_gwpcasearchivescreen_028}Year {VAR_1}, {VAR_2}, day {VAR_3}, {VAR_4}:00",
                "VAR_1", time.GetYear,
                "VAR_2", season,
                "VAR_3", time.GetDayOfSeason + 1,
                "VAR_4", time.GetHourOfDay);
        }

        private static string FormatLocation(Vec2 position)
        {
            var nearestTown = GwpCommon.FindNearestTown(position);
            if (nearestTown != null)
            {
                return GwpText.Get(
                    "{=gwp_gwpcasearchivescreen_029}near {VAR_1} ({VAR_2}, {VAR_3})",
                    "VAR_1", nearestTown.Name?.ToString() ?? GwpText.Get("{=gwp_gwpcasearchivescreen_031}Unknown town"),
                    "VAR_2", GwpText.Format(position.x, "0.0"),
                    "VAR_3", GwpText.Format(position.y, "0.0"));
            }

            return GwpText.Get(
                "{=gwp_gwpcasearchivescreen_030}wilderness ({VAR_1}, {VAR_2})",
                "VAR_1", GwpText.Format(position.x, "0.0"),
                "VAR_2", GwpText.Format(position.y, "0.0"));
        }
    }
}
