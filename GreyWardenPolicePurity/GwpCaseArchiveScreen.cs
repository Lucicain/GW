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
        private string _treasuryText = string.Empty;
        private string _reputationText = string.Empty;
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
        public string TreasuryText
        {
            get => _treasuryText;
            set
            {
                if (value == _treasuryText) return;
                _treasuryText = value;
                OnPropertyChangedWithValue(value, nameof(TreasuryText));
            }
        }

        /// <summary>
        /// The player's own standing with the Grey Wardens.  Until now it only
        /// ever appeared as a toast at the instant it changed, so a player who
        /// looked away had no way to find out where they stood; the ledger is
        /// the one screen that already exists to answer that kind of question.
        /// </summary>
        [DataSourceProperty]
        public string ReputationText
        {
            get => _reputationText;
            set
            {
                if (value == _reputationText) return;
                _reputationText = value;
                OnPropertyChangedWithValue(value, nameof(ReputationText));
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
            var reconstructionTasks = GreyWardenVillageReconstructionBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.QueuedTime.ToHours)
                .ThenBy(task => task.VillageSettlementId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var issueTasks = GreyWardenIssueResolutionBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.QueuedTime.ToHours)
                .ThenBy(task => task.IssueId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var trainingTasks = GreyWardenTrainingBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.QueuedTime.ToHours)
                .ThenBy(task => task.TrainerPartyId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var playerFiefRequests = GreyWardenPlayerRequestBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.FiledTime.ToHours)
                .ThenBy(task => task.SettlementName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var playerTroopOrders = GreyWardenTroopRequestBehavior.GetTaskSnapshots()
                .OrderByDescending(task => task.FiledTime.ToHours)
                .ThenBy(task => task.TrainerPartyId, StringComparer.OrdinalIgnoreCase)
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
            assignedRows.AddRange(reconstructionTasks.Where(task => task.IsAssigned).Select(task =>
                (task.QueuedTime.ToHours, "rebuild:" + task.PolicePartyId,
                    new GwpCaseArchiveItemVM(task))));
            assignedRows.AddRange(issueTasks.Where(task => task.IsAssigned).Select(task =>
                (task.QueuedTime.ToHours, "issue:" + task.IssueId,
                    new GwpCaseArchiveItemVM(task))));
            assignedRows.AddRange(trainingTasks.Where(task => task.IsAssigned).Select(task =>
                (task.QueuedTime.ToHours, "training:" + task.TrainerPartyId,
                    new GwpCaseArchiveItemVM(task))));
            assignedRows.AddRange(playerFiefRequests.Select(task =>
                (task.FiledTime.ToHours, "player-fief:" + task.SettlementName,
                    new GwpCaseArchiveItemVM(task))));
            assignedRows.AddRange(playerTroopOrders.Select(task =>
                (task.FiledTime.ToHours, "player-troops:" + task.TrainerPartyId,
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
            foreach (GreyWardenVillageReconstructionBehavior.ReconstructionTaskSnapshot task in
                     reconstructionTasks.Where(task => !task.IsAssigned))
                Cases.Add(new GwpCaseArchiveItemVM(task));
            foreach (GreyWardenIssueResolutionBehavior.IssueTaskSnapshot task in
                     issueTasks.Where(task => !task.IsAssigned))
                Cases.Add(new GwpCaseArchiveItemVM(task));
            foreach (GreyWardenTrainingBehavior.TrainingTaskSnapshot task in
                     trainingTasks.Where(task => !task.IsAssigned))
                Cases.Add(new GwpCaseArchiveItemVM(task));

            Summary = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_005}Task pool: ordinary cases {VAR_1}/100 | Other tasks {VAR_2} (uncapped) | Assigned: {VAR_3} | Waiting: {VAR_4}",
                "VAR_1", assignedCases.Count + unassignedCases.Count,
                "VAR_2", assistanceTasks.Count + reliefTasks.Count +
                    reconstructionTasks.Count + issueTasks.Count +
                    trainingTasks.Count + playerFiefRequests.Count +
                    playerTroopOrders.Count,
                "VAR_3", assignedCases.Count + assistanceTasks.Count +
                    reliefTasks.Count(task => task.IsAssigned) +
                    reconstructionTasks.Count(task => task.IsAssigned) +
                    issueTasks.Count(task => task.IsAssigned) +
                    trainingTasks.Count(task => task.IsAssigned) + playerFiefRequests.Count +
                    playerTroopOrders.Count,
                "VAR_4", unassignedCases.Count + reliefTasks.Count(task => !task.IsAssigned) +
                    reconstructionTasks.Count(task => !task.IsAssigned) +
                    issueTasks.Count(task => !task.IsAssigned) +
                    trainingTasks.Count(task => !task.IsAssigned));
            TreasuryText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_056}Judicial treasury: {VAR_1} denars",
                "VAR_1", PoliceResourceManager.GetJudicialTreasuryBalance());
            ReputationText = BuildReputationText();
            IsEmpty = Cases.Count == 0;
        }

        /// <summary>
        /// Standing, the band it falls in, and - while wanted - what settling
        /// it would cost, since the lawful fine is derived from standing and a
        /// player who is being hunted wants to know the number before meeting
        /// a Warden rather than during the conversation.
        /// </summary>
        private static string BuildReputationText()
        {
            int reputation = GwpRuntimeState.Player.Reputation;

            if (GwpRuntimeState.Player.IsWanted)
            {
                // A patrol and a Warden lord levy different rates, so quote
                // both rather than a single number the player cannot match to
                // whoever actually stops them.
                return GwpText.Get(
                    "{=gwp_gwpcasearchivescreen_057}Your standing: {VAR_1} | Wanted — the lawful fine is {VAR_2} denars to a provost patrol, {VAR_3} to a Grey Warden lord",
                    "VAR_1", reputation,
                    "VAR_2", Math.Abs(reputation) * GwpTuning.Patrol.FinePerPoint,
                    "VAR_3", Math.Abs(reputation) * GwpTuning.Enforcement.FinePerPoint);
            }

            string band = reputation > 0
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_058}in good standing")
                : reputation == 0
                    ? GwpText.Get("{=gwp_gwpcasearchivescreen_059}nothing on record")
                    : GwpText.Get("{=gwp_gwpcasearchivescreen_060}under suspicion, not yet wanted");

            return GwpText.Get(
                "{=gwp_gwpcasearchivescreen_061}Your standing: {VAR_1} | {VAR_2}",
                "VAR_1", reputation,
                "VAR_2", band);
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
                "{=gwp_gwpcasearchivescreen_008}{VAR_1} — {VAR_2}",
                "VAR_1", offenderName,
                "VAR_2", status);

            TimeText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_009}Last offence: {VAR_1} | Case opened: {VAR_2}",
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
                "{=gwp_gwpcasearchivescreen_012}Offence: {VAR_1} | Victim: {VAR_2} | Location: {VAR_3}",
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
                                leader?.Name?.ToString() ??
                                GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");
            string helperName = helper?.LeaderHero?.Name?.ToString() ??
                                helper?.Name?.ToString() ??
                                GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");
            CrimeRecord? sourceCase = CrimePool.GetRecordByKey(assistance.CrimeId);
            string targetName = sourceCase != null
                ? ResolveOffenderName(sourceCase)
                : target?.LeaderHero?.Name?.ToString() ??
                  target?.Name?.ToString() ??
                  GwpText.Get("{=gwp_gwpcasearchivescreen_048}Unknown target");
            string crimeType = sourceCase == null || string.IsNullOrWhiteSpace(sourceCase.CrimeType)
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_010}Unrecorded")
                : GwpText.CrimeType(sourceCase.CrimeType);

            Header = GwpText.Get("{=gwp_gwpcasearchivescreen_032}Assistance — {VAR_1}",
                "VAR_1", targetName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_033}Assigned: {VAR_1}",
                "VAR_1", FormatCampaignDate(assistance.AssignedTime));
            AssignmentText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_034}Assignee: {VAR_1} | Supporting: {VAR_2}",
                "VAR_1", helperName, "VAR_2", leaderName);
            DetailsText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_035}Assistance reason: the lead Grey Warden's pursuit was blocked and requires a joint capture | Source case: {VAR_1} — {VAR_2}",
                "VAR_1", targetName, "VAR_2", crimeType);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenVillageAdoptionBehavior.VillageReliefTaskSnapshot relief)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, relief.PolicePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = party?.LeaderHero?.Name?.ToString() ??
                              party?.Name?.ToString() ??
                              GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");

            Header = GwpText.Get("{=gwp_gwpcasearchivescreen_036}Village relief — {VAR_1}",
                "VAR_1", relief.VillageName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(relief.QueuedTime));
            AssignmentText = relief.IsAssigned
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_038}Assignee: {VAR_1} | Stage: {VAR_2}",
                    "VAR_1", assignee, "VAR_2", DescribeReliefStage(relief.Stage))
                : GwpText.Get("{=gwp_gwpcasearchivescreen_039}Assignee: waiting for forced assignment");
            DetailsText = relief.Stage == GreyWardenVillageAdoptionBehavior.ReliefStage.StayingInVillage
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_040}Task type: forced adoption relief | Remaining: {VAR_1} hours",
                    "VAR_1", Math.Ceiling(relief.RemainingHours))
                : GwpText.Get("{=gwp_gwpcasearchivescreen_041}Task type: forced adoption relief");
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenVillageReconstructionBehavior.ReconstructionTaskSnapshot reconstruction)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, reconstruction.PolicePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = party?.LeaderHero?.Name?.ToString() ??
                              party?.Name?.ToString() ??
                              GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");

            Header = GwpText.Get("{=gwp_gwpcasearchivescreen_049}Village reconstruction — {VAR_1}",
                "VAR_1", reconstruction.VillageName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(reconstruction.QueuedTime));
            AssignmentText = reconstruction.IsAssigned
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_050}Assignee: {VAR_1} ({VAR_2}) | Stage: {VAR_3}",
                    "VAR_1", assignee,
                    "VAR_2", GreyWardenFamilyBehavior.GetDutyTitle(party?.LeaderHero),
                    "VAR_3", DescribeReconstructionStage(reconstruction.Stage))
                : GreyWardenFamilyBehavior.HasLivingReconstructionHolder()
                    ? GwpText.Get("{=gwp_gwpcasearchivescreen_051}Assignee: waiting for a reconstruction warden")
                    : GwpText.Get("{=gwp_gwpcasearchivescreen_052}Assignee: reconstruction office has died out");
            DetailsText = reconstruction.Stage ==
                          GreyWardenVillageReconstructionBehavior.ReconstructionStage.Rebuilding
                ? GwpText.Get("{=gwp_gwpcasearchivescreen_053}Task type: village reconstruction | Remaining: {VAR_1} hours | Estimated allocation: {VAR_2} | Treasury reserve: {VAR_3}",
                    "VAR_1", Math.Ceiling(reconstruction.RemainingHours),
                    "VAR_2", reconstruction.EstimatedCost,
                    "VAR_3", reconstruction.TreasuryReserve)
                : GwpText.Get("{=gwp_gwpcasearchivescreen_054}Task type: village reconstruction | Estimated allocation: {VAR_1} | Treasury reserve: {VAR_2}",
                    "VAR_1", reconstruction.EstimatedCost,
                    "VAR_2", reconstruction.TreasuryReserve);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenIssueResolutionBehavior.IssueTaskSnapshot issue)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, issue.PolicePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = party?.LeaderHero?.Name?.ToString() ??
                              party?.Name?.ToString() ??
                              GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");
            Header = GwpText.Get("{=gwp_issue_ledger_header}Petition — {VAR_1}",
                "VAR_1", issue.IssueTitle);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(issue.QueuedTime));
            AssignmentText = issue.IsAssigned
                ? GwpText.Get("{=gwp_issue_ledger_assignment}Assignee: {VAR_1} ({VAR_2}) | Stage: {VAR_3}",
                    "VAR_1", assignee,
                    "VAR_2", GreyWardenFamilyBehavior.GetDutyTitle(party?.LeaderHero),
                    "VAR_3", DescribeIssueDutyStage(issue.Stage))
                : GwpText.Get("{=gwp_issue_ledger_waiting}Assignee: waiting in the uncapped petition pool");
            DetailsText = issue.Stage == GreyWardenIssueResolutionBehavior.IssueDutyStage.ReviewingPetition
                ? GwpText.Get("{=gwp_issue_ledger_review}Task type: native town/village issue | Issuer: {VAR_1} | Settlement: {VAR_2} | Remaining review: {VAR_3} hours",
                    "VAR_1", issue.OwnerName, "VAR_2", issue.SettlementName,
                    "VAR_3", Math.Ceiling(issue.RemainingHours))
                : GwpText.Get("{=gwp_issue_ledger_detail}Task type: native town/village issue | Issuer: {VAR_1} | Settlement: {VAR_2}",
                    "VAR_1", issue.OwnerName, "VAR_2", issue.SettlementName);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenTrainingBehavior.TrainingTaskSnapshot training)
        {
            MobileParty? trainer = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, training.TrainerPartyId,
                    StringComparison.OrdinalIgnoreCase));
            MobileParty? target = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, training.TargetPartyId,
                    StringComparison.OrdinalIgnoreCase));
            string trainerName = trainer?.LeaderHero?.Name?.ToString() ??
                                 trainer?.Name?.ToString() ??
                                 GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");
            string targetName = target?.LeaderHero?.Name?.ToString() ??
                                target?.Name?.ToString() ??
                                GwpText.Get("{=gwp_training_unassigned_recipient}Not yet assigned");

            Header = GwpText.Get("{=gwp_training_ledger_header}Training exchange — {VAR_1}",
                "VAR_1", targetName);
            TimeText = GwpText.Get("{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(training.QueuedTime));
            AssignmentText = GwpText.Get(
                "{=gwp_training_ledger_assignment}Trainer: {VAR_1} | Receiving lord: {VAR_2} | Stage: {VAR_3}",
                "VAR_1", trainerName, "VAR_2", targetName,
                "VAR_3", DescribeTrainingStage(training.Stage));
            DetailsText = training.Stage switch
            {
                GreyWardenTrainingBehavior.TrainingTaskStage.QueuedForTrainer =>
                    GwpText.Get("{=gwp_training_ledger_queued}Task type: troop training exchange | Queued as the Training Warden's next duty"),
                GreyWardenTrainingBehavior.TrainingTaskStage.WaitingForCurrentDuties =>
                    GwpText.Get("{=gwp_training_ledger_waiting}Task type: troop training exchange | Rendezvous: {VAR_1} | Waiting for current duties to finish",
                        "VAR_1", training.SettlementName),
                GreyWardenTrainingBehavior.TrainingTaskStage.ExchangingAtSettlement =>
                    GwpText.Get("{=gwp_training_ledger_stay}Task type: troop training exchange | Settlement: {VAR_1} | Remaining stay: {VAR_2} hours",
                        "VAR_1", training.SettlementName,
                        "VAR_2", Math.Ceiling(training.RemainingHours)),
                _ => GwpText.Get("{=gwp_training_ledger_travel}Task type: troop training exchange | Rendezvous: {VAR_1}",
                    "VAR_1", training.SettlementName)
            };
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenPlayerRequestBehavior.PlayerRequestTaskSnapshot request)
        {
            MobileParty? coordinator = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, request.AssigneePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = coordinator?.LeaderHero?.Name?.ToString() ??
                              coordinator?.Name?.ToString() ??
                              GwpText.Get("{=gwp_player_request_unknown_liaison}Unknown Noble Affairs Liaison");

            Header = GwpText.Get(
                "{=gwp_player_request_ledger_header}Player fief appeal — {VAR_1}",
                "VAR_1", request.SettlementName);
            TimeText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(request.FiledTime));
            AssignmentText = GwpText.Get(
                "{=gwp_player_request_ledger_assignment}Liaison: {VAR_1} | Stage: {VAR_2}",
                "VAR_1", assignee,
                "VAR_2", request.DeferredTasksRemaining > 0
                    ? GwpText.Get(
                        "{=gwp_player_request_stage_deferred}Petition set aside")
                    : DescribeFiefRequestStage(request.Stage));
            DetailsText = request.Stage ==
                          GreyWardenPlayerRequestBehavior.FiefRequestStage.LobbyingAtSettlement
                ? GwpText.Get(
                    "{=gwp_player_request_ledger_lobbying}Task type: player fief appeal | Fee: {VAR_1} | Public support: {VAR_2}% | Remaining stay: {VAR_3} hours",
                    "VAR_1", request.FeePaid
                        ? GwpText.Get("{=gwp_player_request_fee_paid}paid into public treasury")
                        : GwpText.Get("{=gwp_player_request_fee_unpaid}not yet collected"),
                    "VAR_2", request.PublicSupportPercent,
                    "VAR_3", Math.Ceiling(request.RemainingHours))
                : GwpText.Get(
                    "{=gwp_player_request_ledger_detail}Task type: player fief appeal | Fee: {VAR_1} | Public support: {VAR_2}%",
                    "VAR_1", request.FeePaid
                        ? GwpText.Get("{=gwp_player_request_fee_paid}paid into public treasury")
                        : GwpText.Get("{=gwp_player_request_fee_unpaid}not yet collected"),
                    "VAR_2", request.PublicSupportPercent);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenTroopRequestBehavior.PlayerTroopOrderSnapshot order)
        {
            MobileParty? trainer = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, order.TrainerPartyId,
                    StringComparison.OrdinalIgnoreCase));
            string trainerName = trainer?.LeaderHero?.Name?.ToString() ??
                                 trainer?.Name?.ToString() ??
                                 GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");

            Header = GwpText.Get(
                "{=gwp_player_troop_ledger_header}Player troop order — {VAR_1} × {VAR_2}",
                "VAR_1", order.Count, "VAR_2", order.TroopName);
            TimeText = GwpText.Get(
                "{=gwp_gwpcasearchivescreen_037}Queued: {VAR_1}",
                "VAR_1", FormatCampaignDate(order.FiledTime));
            AssignmentText = GwpText.Get(
                "{=gwp_player_troop_ledger_assignment}Trainer: {VAR_1} | Stage: {VAR_2}",
                "VAR_1", trainerName,
                "VAR_2", order.DeferredTasksRemaining > 0
                    ? GwpText.Get(
                        "{=gwp_player_troop_stage_deferred}Delivery postponed")
                    : DescribePlayerTroopOrderStage(order.Stage));
            DetailsText = GwpText.Get(
                "{=gwp_player_troop_ledger_detail}Task type: player troop order | Ready: {VAR_1}/{VAR_2} | Delivery price: {VAR_3} denars",
                "VAR_1", order.ReadyCount, "VAR_2", order.Count,
                "VAR_3", order.Price);
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
            string partyName = party?.Name?.ToString() ??
                               GwpText.Get("{=gwp_gwpcasearchivescreen_047}Unknown Grey Warden party");
            string? leaderName = party?.LeaderHero?.Name?.ToString();
            string assignee = string.IsNullOrWhiteSpace(leaderName)
                ? partyName
                : GwpText.Get("{=gwp_gwpcasearchivescreen_015}{VAR_1} ({VAR_2})", "VAR_1", leaderName!, "VAR_2", partyName);

            return GwpText.Get(
                "{=gwp_gwpcasearchivescreen_016}Tracking: {VAR_1} | Stage: {VAR_2}",
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
                   ?? GwpText.Get("{=gwp_gwpcasearchivescreen_048}Unknown target");
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

        private static string DescribeReconstructionStage(
            GreyWardenVillageReconstructionBehavior.ReconstructionStage stage) => stage switch
        {
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.WaitingForAssignment =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_042}waiting for assignment"),
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.TravelingToVillage =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_044}travelling to village"),
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.Rebuilding =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_055}reconstruction in progress"),
            _ => GwpText.Get("{=gwp_gwpcasearchivescreen_046}unknown")
        };

        private static string DescribeIssueDutyStage(
            GreyWardenIssueResolutionBehavior.IssueDutyStage stage) => stage switch
        {
            GreyWardenIssueResolutionBehavior.IssueDutyStage.WaitingForAssignment =>
                GwpText.Get("{=gwp_gwpcasearchivescreen_042}waiting for assignment"),
            GreyWardenIssueResolutionBehavior.IssueDutyStage.TravelingToIssuer =>
                GwpText.Get("{=gwp_issue_stage_travel}travelling to the issuer"),
            GreyWardenIssueResolutionBehavior.IssueDutyStage.ReviewingPetition =>
                GwpText.Get("{=gwp_issue_stage_review}reviewing for six hours"),
            _ => GwpText.Get("{=gwp_gwpcasearchivescreen_046}unknown")
        };

        private static string DescribeTrainingStage(
            GreyWardenTrainingBehavior.TrainingTaskStage stage) => stage switch
        {
            GreyWardenTrainingBehavior.TrainingTaskStage.QueuedForTrainer =>
                GwpText.Get("{=gwp_training_stage_queued}queued as next duty"),
            GreyWardenTrainingBehavior.TrainingTaskStage.WaitingForCurrentDuties =>
                GwpText.Get("{=gwp_training_stage_waiting}waiting for current duties"),
            GreyWardenTrainingBehavior.TrainingTaskStage.TravelingToRendezvous =>
                GwpText.Get("{=gwp_training_stage_travel}travelling to rendezvous"),
            GreyWardenTrainingBehavior.TrainingTaskStage.ExchangingAtSettlement =>
                GwpText.Get("{=gwp_training_stage_stay}two-hour troop exchange"),
            _ => GwpText.Get("{=gwp_gwpcasearchivescreen_046}unknown")
        };

        private static string DescribeFiefRequestStage(
            GreyWardenPlayerRequestBehavior.FiefRequestStage stage) => stage switch
        {
            GreyWardenPlayerRequestBehavior.FiefRequestStage.SeekingPlayerForPayment =>
                GwpText.Get("{=gwp_player_request_stage_payment}travelling to collect payment"),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.TravelingToSettlement =>
                GwpText.Get("{=gwp_player_request_stage_travel}travelling to disputed fief"),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.LobbyingAtSettlement =>
                GwpText.Get("{=gwp_player_request_stage_lobbying}organizing public petition"),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.AwaitingVote =>
                GwpText.Get("{=gwp_player_request_stage_vote}new fief vote opened"),
            _ => GwpText.Get("{=gwp_gwpcasearchivescreen_046}unknown")
        };

        private static string DescribePlayerTroopOrderStage(
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage stage) => stage switch
        {
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage.Training =>
                GwpText.Get("{=gwp_player_troop_stage_training}training real troops"),
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage.Delivering =>
                GwpText.Get("{=gwp_player_troop_stage_delivery}travelling to deliver troops"),
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
