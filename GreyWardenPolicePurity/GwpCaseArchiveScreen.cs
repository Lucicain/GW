using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
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

        internal static bool IsOpen => _activeOverlay != null;

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
        private string _familyText = string.Empty;
        private string _emptyText = string.Empty;
        private string _refreshText = string.Empty;
        private string _closeText = string.Empty;
        private bool _isEmpty;

        public GwpCaseArchiveVM(Action onClose)
        {
            _onClose = onClose;
            Cases = new MBBindingList<GwpCaseArchiveItemVM>();
            Title = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_001);
            EmptyText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_002);
            RefreshText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_003);
            CloseText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_004);
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

        /// <summary>
        /// Family size, adoption cooldown and the last adoption.  This used to
        /// sit at the top of the war-grounds popup, where it had nothing to do
        /// with grounds for war; the running relief task it also listed is a
        /// row in this very list, and a fuller one.
        /// </summary>
        [DataSourceProperty]
        public string FamilyText
        {
            get => _familyText;
            set
            {
                if (value == _familyText) return;
                _familyText = value;
                OnPropertyChangedWithValue(value, nameof(FamilyText));
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

            // Standing wars lead the list.  They are the one thing here that is
            // not a task, and they explain why several of the tasks below are
            // being prosecuted across a border at all.
            foreach (GwpPoliceWarReasonService.WarReasonEntry war in
                     GwpPoliceWarReasonService.GetCurrentWarReasonEntries())
            {
                Cases.Add(new GwpCaseArchiveItemVM(war));
            }

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
            // 最近几次使者回报（用户裁定 2026-10-16：显示使者进度和结果）。案子结了卷宗就没了，回报单独成行。
            foreach (PoliceEnforcementBehavior.EnvoyReport report in PoliceEnforcementBehavior.GetEnvoyReports())
                Cases.Add(new GwpCaseArchiveItemVM(report));
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
                GwpTextCatalog.gwp_gwpcasearchivescreen_005,
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
                GwpTextCatalog.gwp_gwpcasearchivescreen_056,
                "VAR_1", PoliceResourceManager.GetJudicialTreasuryBalance());
            ReputationText = BuildReputationText();
            FamilyText = BuildFamilyText();
            IsEmpty = Cases.Count == 0;
        }

        private static string BuildFamilyText()
        {
            if (!GreyWardenVillageAdoptionBehavior.TryGetAdoptionStatus(out var status))
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_062);

            string cooldown = status.IsCooldownReady
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_063)
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_064,
                    "VAR_1", FormatRemainingHours(status.RemainingCooldownHours));

            string last = status.HasRecordedAdoption
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_065,
                    "VAR_1", GwpCaseArchiveItemVM.FormatCampaignDate(
                        CampaignTime.Hours((float)status.LastAdoptionTimeHours)))
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_066);

            return GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_067,
                "VAR_1", status.LivingMembers,
                "VAR_2", status.MaxMembers,
                "VAR_3", cooldown,
                "VAR_4", last);
        }

        private static string FormatRemainingHours(double hours)
        {
            if (hours <= 0d)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_068);

            int days = (int)(hours / 24d);
            return days >= 1
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_069, "VAR_1", days)
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_070,
                    "VAR_1", Math.Max(1, (int)Math.Ceiling(hours)));
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
                    GwpTextCatalog.gwp_gwpcasearchivescreen_057,
                    "VAR_1", reputation,
                    "VAR_2", GwpFieldArrestPricing.StandingFine(reputation, GwpTuning.Patrol.FinePerPoint),
                    "VAR_3", GwpFieldArrestPricing.StandingFine(reputation, GwpTuning.Enforcement.FinePerPoint));
            }

            string band = reputation > 0
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_058)
                : reputation == 0
                    ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_059)
                    : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_060);

            return GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_061,
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

        /// <summary>
        /// A standing war and one ground for it.  Grounds that rest on a case
        /// name it rather than restate it, because that case has its own row
        /// further down this same list.
        /// </summary>
        internal GwpCaseArchiveItemVM(GwpPoliceWarReasonService.WarReasonEntry war)
        {
            Header = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_071,
                "VAR_1", war.Faction?.Name?.ToString()
                    ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_072));
            TimeText = war.Total > 1
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_073,
                    "VAR_1", war.Ordinal, "VAR_2", war.Total)
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_074);
            AssignmentText = war.Detail ?? string.Empty;
            DetailsText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_075);
        }

        public GwpCaseArchiveItemVM(CrimeRecord record, PoliceTask? task)
        {
            string offenderName = ResolveOffenderName(record);
            string status = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_006);
            Header = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_008,
                "VAR_1", offenderName,
                "VAR_2", status);

            TimeText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_009,
                "VAR_1", FormatCampaignDate(record.LastCrimeTime),
                "VAR_2", FormatCampaignDate(record.OccurredTime));

            AssignmentText = AppendEnvoyStatus(AppendHelperPause(BuildAssignmentText(record, task), task), record);

            string crimeType = string.IsNullOrWhiteSpace(record.CrimeType)
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_010)
                : GwpText.CrimeType(record.CrimeType);
            string victim = string.IsNullOrWhiteSpace(record.VictimName)
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_011)
                : record.VictimName;
            DetailsText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_012,
                "VAR_1", crimeType,
                "VAR_2", victim,
                "VAR_3", FormatLocation(record.Location));
        }

        /// <summary>一次使者谈判的结果。</summary>
        internal GwpCaseArchiveItemVM(PoliceEnforcementBehavior.EnvoyReport report)
        {
            string offenderName = Hero.Find(report.OffenderHeroId)?.Name?.ToString()
                                  ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_048);
            string wardenName = Hero.Find(report.WardenHeroId)?.Name?.ToString()
                                ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            Header = GwpText.Get(GwpTextCatalog.gwp_envoy_report_header, "VAR_1", offenderName);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_envoy_report_time,
                "VAR_1", FormatCampaignDate(CampaignTime.Hours((float)report.Hours)));
            AssignmentText = GwpText.Get(GwpTextCatalog.gwp_envoy_report_warden, "VAR_1", wardenName);
            DetailsText = DescribeEnvoyOutcome(report);
        }

        private static string DescribeEnvoyOutcome(PoliceEnforcementBehavior.EnvoyReport report) => report.Outcome switch
        {
            "grace" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_grace),
            "duel_called" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_duel_called,
                "VAR_1", Settlement.Find(report.TownId)?.Name?.ToString() ?? string.Empty),
            "paid" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_paid, "VAR_1", report.Amount),
            "paid_share" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_paid_share, "VAR_1", report.Amount),
            "surrender" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_surrender),
            "bribed" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_bribed, "VAR_1", report.Amount),
            "duel_won" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_duel_won, "VAR_1", report.Amount),
            "duel_lost" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_duel_lost),
            "refused" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_refused),
            "talks_failed" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_talks_failed),
            "grace_broke" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_grace_broke),
            "lost" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_lost),
            "timeout" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_timeout),
            "duel_cancelled" => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_duel_cancelled),
            _ => GwpText.Get(GwpTextCatalog.gwp_envoy_outcome_failed)
        };

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
                                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            string helperName = helper?.LeaderHero?.Name?.ToString() ??
                                helper?.Name?.ToString() ??
                                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            CrimeRecord? sourceCase = CrimePool.GetRecordByKey(assistance.CrimeId);
            string targetName = sourceCase != null
                ? ResolveOffenderName(sourceCase)
                : target?.LeaderHero?.Name?.ToString() ??
                  target?.Name?.ToString() ??
                  GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_048);
            string crimeType = sourceCase == null || string.IsNullOrWhiteSpace(sourceCase.CrimeType)
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_010)
                : GwpText.CrimeType(sourceCase.CrimeType);

            Header = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_032,
                "VAR_1", targetName);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_033,
                "VAR_1", FormatCampaignDate(assistance.AssignedTime));
            AssignmentText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_034,
                "VAR_1", helperName, "VAR_2", leaderName);
            DetailsText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_035,
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
                              GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);

            Header = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_036,
                "VAR_1", relief.VillageName);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(relief.QueuedTime));
            AssignmentText = relief.IsAssigned
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_038,
                    "VAR_1", assignee, "VAR_2", DescribeReliefStage(relief.Stage))
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_039);
            DetailsText = relief.Stage == GreyWardenVillageAdoptionBehavior.ReliefStage.StayingInVillage
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_040,
                    "VAR_1", Math.Ceiling(relief.RemainingHours))
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_041);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenVillageReconstructionBehavior.ReconstructionTaskSnapshot reconstruction)
        {
            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, reconstruction.PolicePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = party?.LeaderHero?.Name?.ToString() ??
                              party?.Name?.ToString() ??
                              GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);

            Header = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_049,
                "VAR_1", reconstruction.VillageName);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(reconstruction.QueuedTime));
            AssignmentText = reconstruction.IsAssigned
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_050,
                    "VAR_1", assignee,
                    "VAR_2", GreyWardenFamilyBehavior.GetDutyTitle(party?.LeaderHero),
                    "VAR_3", DescribeReconstructionStage(reconstruction.Stage))
                : GreyWardenFamilyBehavior.HasLivingReconstructionHolder()
                    ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_051)
                    : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_052);
            DetailsText = reconstruction.Stage ==
                          GreyWardenVillageReconstructionBehavior.ReconstructionStage.Rebuilding
                ? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_053,
                    "VAR_1", Math.Ceiling(reconstruction.RemainingHours),
                    "VAR_2", reconstruction.EstimatedCost,
                    "VAR_3", reconstruction.TreasuryReserve)
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_054,
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
                              GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            Header = GwpText.Get(GwpTextCatalog.gwp_issue_ledger_header,
                "VAR_1", issue.IssueTitle);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(issue.QueuedTime));
            AssignmentText = issue.IsAssigned
                ? GwpText.Get(GwpTextCatalog.gwp_issue_ledger_assignment,
                    "VAR_1", assignee,
                    "VAR_2", GreyWardenFamilyBehavior.GetDutyTitle(party?.LeaderHero),
                    "VAR_3", DescribeIssueDutyStage(issue.Stage))
                : GwpText.Get(GwpTextCatalog.gwp_issue_ledger_waiting);
            DetailsText = issue.Stage == GreyWardenIssueResolutionBehavior.IssueDutyStage.ReviewingPetition
                ? GwpText.Get(GwpTextCatalog.gwp_issue_ledger_review,
                    "VAR_1", issue.OwnerName, "VAR_2", issue.SettlementName,
                    "VAR_3", Math.Ceiling(issue.RemainingHours))
                : GwpText.Get(GwpTextCatalog.gwp_issue_ledger_detail,
                    "VAR_1", issue.OwnerName, "VAR_2", issue.SettlementName);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenTrainingBehavior.TrainingTaskSnapshot training)
        {
            MobileParty? trainer = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, training.TrainerPartyId,
                    StringComparison.OrdinalIgnoreCase));
            string trainerName = trainer?.LeaderHero?.Name?.ToString() ??
                                 trainer?.Name?.ToString() ??
                                 GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            Header = GwpText.Get(GwpTextCatalog.gwp_training_ledger_header,
                "VAR_1", training.SettlementName);
            TimeText = GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(training.QueuedTime));
            AssignmentText = GwpText.Get(
                GwpTextCatalog.gwp_training_ledger_assignment,
                "VAR_1", trainerName, "VAR_2", DescribeTrainingStage(training.Stage));
            DetailsText = GwpText.Get(training.Stage == GreyWardenTrainingBehavior.TrainingTaskStage.SettlingInVillage
                    ? GwpTextCatalog.gwp_training_ledger_settle : GwpTextCatalog.gwp_training_ledger_travel,
                "VAR_1", training.SettlementName);
        }

        internal GwpCaseArchiveItemVM(
            GreyWardenPlayerRequestBehavior.PlayerRequestTaskSnapshot request)
        {
            MobileParty? coordinator = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, request.AssigneePartyId,
                    StringComparison.OrdinalIgnoreCase));
            string assignee = coordinator?.LeaderHero?.Name?.ToString() ??
                              coordinator?.Name?.ToString() ??
                              GwpText.Get(GwpTextCatalog.gwp_player_request_unknown_liaison);

            Header = GwpText.Get(
                GwpTextCatalog.gwp_player_request_ledger_header,
                "VAR_1", request.SettlementName);
            TimeText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(request.FiledTime));
            AssignmentText = GwpText.Get(
                GwpTextCatalog.gwp_player_request_ledger_assignment,
                "VAR_1", assignee,
                "VAR_2", request.DeferredTasksRemaining > 0
                    ? GwpText.Get(
                        GwpTextCatalog.gwp_player_request_stage_deferred)
                    : DescribeFiefRequestStage(request.Stage));
            DetailsText = request.Stage ==
                          GreyWardenPlayerRequestBehavior.FiefRequestStage.LobbyingAtSettlement
                ? GwpText.Get(
                    GwpTextCatalog.gwp_player_request_ledger_lobbying,
                    "VAR_1", request.FeePaid
                        ? GwpText.Get(GwpTextCatalog.gwp_player_request_fee_paid)
                        : GwpText.Get(GwpTextCatalog.gwp_player_request_fee_unpaid),
                    "VAR_2", request.PublicSupportPercent,
                    "VAR_3", Math.Ceiling(request.RemainingHours))
                : GwpText.Get(
                    GwpTextCatalog.gwp_player_request_ledger_detail,
                    "VAR_1", request.FeePaid
                        ? GwpText.Get(GwpTextCatalog.gwp_player_request_fee_paid)
                        : GwpText.Get(GwpTextCatalog.gwp_player_request_fee_unpaid),
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
                                 GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);

            Header = GwpText.Get(
                GwpTextCatalog.gwp_player_troop_ledger_header,
                "VAR_1", order.Count, "VAR_2", order.TroopName);
            TimeText = GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_037,
                "VAR_1", FormatCampaignDate(order.FiledTime));
            AssignmentText = GwpText.Get(
                GwpTextCatalog.gwp_player_troop_ledger_assignment,
                "VAR_1", trainerName,
                "VAR_2", DescribePlayerTroopOrderStage(order.Stage));
            DetailsText = GwpText.Get(
                GwpTextCatalog.gwp_player_troop_ledger_detail,
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
            // 玩家接了这宗案子，卷宗上的主理人就是玩家。求援之后到场的那名灰袍是支援，
            // 不是主理人——虽然内部仍由他的任务驱动行动，档案上要按职责如实写。
            if (GwpPlayerCaseAssignment.IsCaseHeldByPlayer(record.OffenderHeroId))
            {
                string commissioner = Hero.MainHero?.Name?.ToString() ??
                    GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_017);
                MobileParty? supporter = task?.IsPlayerBountyEscort == true
                    ? MobileParty.All.FirstOrDefault(candidate => string.Equals(
                        candidate.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (supporter == null)
                    return GwpText.Get(
                        GwpTextCatalog.gwp_case_owner_player,
                        "VAR_1", commissioner);
                string supporterName = supporter.LeaderHero?.Name?.ToString() ??
                                       supporter.Name?.ToString() ??
                                       GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
                return GwpText.Get(
                    GwpTextCatalog.gwp_case_owner_player_supported,
                    "VAR_1", commissioner, "VAR_2", supporterName,
                    "VAR_3", DescribeTaskStage(task!));
            }

            if (task == null)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_013);

            MobileParty? party = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase));
            string partyName = party?.Name?.ToString() ??
                               GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_047);
            string? leaderName = party?.LeaderHero?.Name?.ToString();
            string assignee = string.IsNullOrWhiteSpace(leaderName)
                ? partyName
                : GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_015, "VAR_1", leaderName!, "VAR_2", partyName);

            return GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_016,
                "VAR_1", assignee,
                "VAR_2", DescribeTaskStage(task));
        }

        /// <summary>承办人正在协助别人：他自己这宗案子暂停（用户裁定 2026-10-16）。</summary>
        private static string AppendHelperPause(string text, PoliceTask? task)
        {
            if (task == null) return text;
            MobileParty? owner = MobileParty.All.FirstOrDefault(candidate =>
                string.Equals(candidate.StringId, task.PolicePartyId, StringComparison.OrdinalIgnoreCase));
            MobileParty? leader = PoliceEnforcementBehavior.AssistingLeaderOf(owner);
            if (leader == null) return text;
            return GwpText.Get(GwpTextCatalog.gwp_case_helper_paused, "VAR_1", text,
                "VAR_2", leader.LeaderHero?.Name?.ToString() ?? leader.Name?.ToString() ?? string.Empty);
        }

        /// <summary>这宗案子的使者走到哪一步了（用户裁定 2026-10-16：显示使者进度和结果）。</summary>
        private static string AppendEnvoyStatus(string text, CrimeRecord record)
        {
            PoliceEnforcementBehavior.EnvoyStatus? found = PoliceEnforcementBehavior.GetEnvoyStatus(record.CrimeId);
            if (found == null) return text;
            PoliceEnforcementBehavior.EnvoyStatus status = found.Value;
            string? stage = status.Stage switch
            {
                PoliceEnforcementBehavior.EnvoyStage.Travelling when status.CollectOnly =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_collecting),
                PoliceEnforcementBehavior.EnvoyStage.Travelling =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_travelling, "VAR_1",
                        Math.Max(1, (int)Math.Ceiling(status.DueHours - CampaignTime.Now.ToHours))),
                PoliceEnforcementBehavior.EnvoyStage.Failed =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_failed),
                PoliceEnforcementBehavior.EnvoyStage.Grace =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_grace, "VAR_1",
                        FormatCampaignDate(CampaignTime.Hours((float)status.DueHours))),
                PoliceEnforcementBehavior.EnvoyStage.Duel or PoliceEnforcementBehavior.EnvoyStage.DuelDecided =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_duel, "VAR_1",
                        Settlement.Find(status.TownId)?.Name?.ToString() ?? string.Empty),
                PoliceEnforcementBehavior.EnvoyStage.WaitingForDuel =>
                    GwpText.Get(GwpTextCatalog.gwp_case_envoy_duel_waiting),
                _ => null
            };
            return stage == null ? text : GwpText.Get(GwpTextCatalog.gwp_case_envoy_suffix, "VAR_1", text, "VAR_2", stage);
        }

        private static string ResolveOffenderName(CrimeRecord record)
        {
            if (string.Equals(record.CrimeId, CrimePool.PlayerCrimeId, StringComparison.OrdinalIgnoreCase))
                return Hero.MainHero?.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_017);

            return record.OffenderHero?.Name?.ToString()
                   ?? record.Offender?.LeaderHero?.Name?.ToString()
                   ?? record.Offender?.Name?.ToString()
                   ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_048);
        }

        private static string DescribeTaskStage(PoliceTask task)
        {
            if (task.IsPlayerBountyEscort)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_018);
            if (task.IsEscortingPlayer)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_019);
            if (task.IsPreparingDispatch)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_020);
            if (task.WarDeclared)
                return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_021);
            return GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_022);
        }

        private static string DescribeReliefStage(
            GreyWardenVillageAdoptionBehavior.ReliefStage stage) => stage switch
        {
            GreyWardenVillageAdoptionBehavior.ReliefStage.WaitingForAssignment =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_042),
            GreyWardenVillageAdoptionBehavior.ReliefStage.Paused =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_043),
            GreyWardenVillageAdoptionBehavior.ReliefStage.TravelingToVillage =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_044),
            GreyWardenVillageAdoptionBehavior.ReliefStage.StayingInVillage =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_045),
            _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_046)
        };

        private static string DescribeReconstructionStage(
            GreyWardenVillageReconstructionBehavior.ReconstructionStage stage) => stage switch
        {
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.Paused =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_043),
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.WaitingForAssignment =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_042),
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.TravelingToVillage =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_044),
            GreyWardenVillageReconstructionBehavior.ReconstructionStage.Rebuilding =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_055),
            _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_046)
        };

        private static string DescribeIssueDutyStage(
            GreyWardenIssueResolutionBehavior.IssueDutyStage stage) => stage switch
        {
            GreyWardenIssueResolutionBehavior.IssueDutyStage.Paused =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_043),
            GreyWardenIssueResolutionBehavior.IssueDutyStage.WaitingForAssignment =>
                GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_042),
            GreyWardenIssueResolutionBehavior.IssueDutyStage.TravelingToIssuer =>
                GwpText.Get(GwpTextCatalog.gwp_issue_stage_travel),
            GreyWardenIssueResolutionBehavior.IssueDutyStage.ReviewingPetition =>
                GwpText.Get(GwpTextCatalog.gwp_issue_stage_review),
            _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_046)
        };

        private static string DescribeTrainingStage(GreyWardenTrainingBehavior.TrainingTaskStage stage) =>
            GwpText.Get(stage == GreyWardenTrainingBehavior.TrainingTaskStage.SettlingInVillage
                    ? GwpTextCatalog.gwp_training_stage_settle : GwpTextCatalog.gwp_training_stage_travel);

        private static string DescribeFiefRequestStage(
            GreyWardenPlayerRequestBehavior.FiefRequestStage stage) => stage switch
        {
            GreyWardenPlayerRequestBehavior.FiefRequestStage.SeekingPlayerForPayment =>
                GwpText.Get(GwpTextCatalog.gwp_player_request_stage_payment),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.TravelingToSettlement =>
                GwpText.Get(GwpTextCatalog.gwp_player_request_stage_travel),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.LobbyingAtSettlement =>
                GwpText.Get(GwpTextCatalog.gwp_player_request_stage_lobbying),
            GreyWardenPlayerRequestBehavior.FiefRequestStage.AwaitingVote =>
                GwpText.Get(GwpTextCatalog.gwp_player_request_stage_vote),
            _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_046)
        };

        private static string DescribePlayerTroopOrderStage(
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage stage) => stage switch
        {
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage.Training =>
                GwpText.Get(GwpTextCatalog.gwp_player_troop_stage_training),
            GreyWardenTroopRequestBehavior.PlayerTroopOrderStage.Delivering =>
                GwpText.Get(GwpTextCatalog.gwp_player_troop_stage_delivery),
            _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_046)
        };

        internal static string FormatCampaignDate(CampaignTime time)
        {
            string season = time.GetSeasonOfYear switch
            {
                CampaignTime.Seasons.Spring => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_023),
                CampaignTime.Seasons.Summer => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_024),
                CampaignTime.Seasons.Autumn => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_025),
                CampaignTime.Seasons.Winter => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_026),
                _ => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_027)
            };

            return GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_028,
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
                    GwpTextCatalog.gwp_gwpcasearchivescreen_029,
                    "VAR_1", nearestTown.Name?.ToString() ?? GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_031),
                    "VAR_2", GwpText.Format(position.x, "0.0"),
                    "VAR_3", GwpText.Format(position.y, "0.0"));
            }

            return GwpText.Get(
                GwpTextCatalog.gwp_gwpcasearchivescreen_030,
                "VAR_1", GwpText.Format(position.x, "0.0"),
                "VAR_2", GwpText.Format(position.y, "0.0"));
        }
    }
}
