using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.CampaignSystem.ViewModelCollection.Input;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;

namespace GreyWardenPolicePurity
{
    public sealed class GwpAffairsChoiceVM : ViewModel
    {
        private readonly Action _select;
        private bool _selected;
        internal GwpAffairsChoiceVM(string id, string title, string summary, Action select, Hero? hero = null, string tabBrush = "Header.Tab.Center")
        {
            Id = id; Title = title; Summary = summary; _select = select;
            Portrait = hero == null ? null : new CharacterImageIdentifierVM(CampaignUIHelper.GetCharacterCode(hero.CharacterObject));
            HasPortrait = hero != null;
            TabBrush = tabBrush;
        }
        internal string Id { get; }
        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public string Summary { get; }
        [DataSourceProperty] public CharacterImageIdentifierVM? Portrait { get; }
        [DataSourceProperty] public bool HasPortrait { get; }
        [DataSourceProperty] public int TextInset => HasPortrait ? 94 : 18;
        [DataSourceProperty] public string TabBrush { get; }
        [DataSourceProperty] public bool IsFirstTab => TabBrush == "Header.Tab.Left";
        [DataSourceProperty] public bool IsMiddleTab => TabBrush == "Header.Tab.Center";
        [DataSourceProperty] public bool IsLastTab => TabBrush == "Header.Tab.Right";
        [DataSourceProperty] public bool IsSelected
        {
            get => _selected;
            set { if (_selected == value) return; _selected = value; OnPropertyChangedWithValue(value, nameof(IsSelected)); }
        }
        public void ExecuteSelect() => _select();
        public override void OnFinalize() { Portrait?.OnFinalize(); base.OnFinalize(); }
    }

    public sealed class GwpAffairsLineVM : ViewModel
    {
        internal GwpAffairsLineVM(string text) { Text = text; }
        [DataSourceProperty] public string Text { get; }
    }

    public sealed class GwpAffairsSectionVM : ViewModel
    {
        internal GwpAffairsSectionVM(string title, IEnumerable<string> lines)
        {
            Title = title;
            foreach (string line in lines.Where(s => !string.IsNullOrWhiteSpace(s))) Lines.Add(new GwpAffairsLineVM(line));
        }
        [DataSourceProperty] public string Title { get; }
        [DataSourceProperty] public MBBindingList<GwpAffairsLineVM> Lines { get; } = new();
    }

    public sealed class GwpAffairsVM : ViewModel
    {
        private readonly Action _close;
        private List<GwpAffairsEntry> _entries = new();
        private List<GwpAffairsEntry> _faith = new();
        private List<GwpAffairsEntry> _visible = new();
        // These are display preferences for this screen lifetime, not campaign work.
        private readonly Dictionary<int, string> _categories = new();
        private readonly Dictionary<string, string> _selections = new();
        private int _page;
        private string _category = "current";
        private GwpAffairsEntry? _selected;
        private string _status = string.Empty;
        private PlayerBountyBehavior? Bounty => Campaign.Current?.GetCampaignBehavior<PlayerBountyBehavior>();
        private string SelectionKey => _page + ":" + _category;

        public GwpAffairsVM(Action close)
        {
            _close = close;
            string[] titles = { GwpTextCatalog.gwp_affairs_mine, GwpTextCatalog.gwp_affairs_members,
                GwpTextCatalog.gwp_affairs_duties, GwpTextCatalog.gwp_affairs_faith, GwpTextCatalog.gwp_affairs_armory };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                Tabs.Add(new GwpAffairsChoiceVM(i.ToString(), GwpText.Get(titles[i]), string.Empty, () => SetPage(index),
                    tabBrush: i == 0 ? "Header.Tab.Left" : i == titles.Length - 1 ? "Header.Tab.Right" : "Header.Tab.Center"));
            }
            Refresh();
            if (!_entries.Any(e => e.Id == "commission"))
                SelectCategory(_entries.Any(e => e.Id.StartsWith("dispatch:")) ? "couriers"
                    : _entries.Any(e => e.Id.StartsWith("order:")) ? "orders" : "current");
        }
        [DataSourceProperty] public MBBindingList<GwpAffairsChoiceVM> Tabs { get; } = new();
        [DataSourceProperty] public MBBindingList<GwpAffairsChoiceVM> Categories { get; } = new();
        [DataSourceProperty] public MBBindingList<GwpAffairsChoiceVM> Rows { get; } = new();
        [DataSourceProperty] public MBBindingList<GwpAffairsLineVM> Fields { get; } = new();
        [DataSourceProperty] public MBBindingList<GwpAffairsSectionVM> Sections { get; } = new();
        [DataSourceProperty] public InputKeyItemVM? DoneInputKey { get; private set; }
        internal void SetDoneInputKey(HotKey key) => DoneInputKey = InputKeyItemVM.CreateFromHotKey(key, true);
        [DataSourceProperty] public string Title => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_001);
        [DataSourceProperty] public string Standing => GwpText.Get(GwpTextCatalog.gwp_affairs_standing, "VALUE", GwpRuntimeState.Player.Reputation);
        [DataSourceProperty] public string ListTitle => Categories.FirstOrDefault(c => c.Id == _category)?.Title ?? string.Empty;
        [DataSourceProperty] public string DetailTitle => _selected?.Title ?? GwpText.Get(_page == 4 ? GwpTextCatalog.gwp_affairs_armory_closed : GwpTextCatalog.gwp_affairs_empty);
        [DataSourceProperty] public string DetailSummary => _selected?.Summary ?? string.Empty;
        [DataSourceProperty] public string EmptyText => GwpText.Get(_page == 4 ? GwpTextCatalog.gwp_affairs_armory_closed : GwpTextCatalog.gwp_affairs_empty);
        [DataSourceProperty] public string Status => !string.IsNullOrEmpty(_status) ? _status
            : ShowAccept && !CanAccept ? Bounty?.GetBountyOfferStatusText() ?? string.Empty : string.Empty;
        [DataSourceProperty] public bool ShowBrowse => _page == 0 && _category == "current" && !_entries.Any(e => e.Id == "commission");
        [DataSourceProperty] public bool ShowRefresh => _page != 4;
        [DataSourceProperty] public string BrowseText => GwpText.Get(GwpTextCatalog.gwp_affairs_browse);
        [DataSourceProperty] public string AcceptText => GwpText.Get(GwpTextCatalog.gwp_affairs_accept);
        [DataSourceProperty] public string HeroText => GwpText.Get(GwpTextCatalog.gwp_affairs_view_hero);
        [DataSourceProperty] public string PlaceText => GwpText.Get(GwpTextCatalog.gwp_affairs_view_place);
        [DataSourceProperty] public string RefreshText => GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_003);
        [DataSourceProperty] public string CloseText => GwpText.Get(GwpTextCatalog.gwp_affairs_done);
        [DataSourceProperty] public bool IsEmpty => Rows.Count == 0;
        [DataSourceProperty] public bool CanAccept => _selected?.IsOffer == true && Bounty?.CanInspectBountyOffers() == true;
        [DataSourceProperty] public bool ShowAccept => _page == 2 && _selected?.IsOffer == true;
        [DataSourceProperty] public bool HasHero => _selected?.Hero != null || _selected?.Assignee != null;
        [DataSourceProperty] public bool HasPlace => _selected?.Place != null;

        private void SetPage(int page)
        {
            _categories[_page] = _category;
            _page = page;
            _category = _categories.TryGetValue(page, out string remembered) ? remembered : page switch
            { 0 => "current", 1 => "Training", 2 => "CaravanProtection", _ => "faith" };
            _status = string.Empty;
            BuildCategories();
        }
        internal void SwitchTab(int direction) => SetPage((_page + direction + Tabs.Count) % Tabs.Count);
        internal void BrowseCases()
        {
            SetPage(2);
            string? duty = _entries.FirstOrDefault(e => e.IsOffer)?.Duty.ToString();
            SelectCategory(duty ?? "CaravanProtection");
        }
        public void ExecuteTakeBounty() => BrowseCases();
        public void ExecuteRefresh() => Refresh();
        public void ExecuteClose() => _close();

        private void Refresh()
        {
            _entries = GwpAffairsData.Read();
            _faith = GwpAffairsData.ReadFaith();
            BuildCategories();
        }

        private void BuildCategories()
        {
            Categories.Clear();
            if (_page == 0)
            {
                AddCategory("current", GwpText.Get(GwpTextCatalog.gwp_affairs_current));
                AddCategory("couriers", GwpText.Get(GwpTextCatalog.gwp_affairs_couriers));
                AddCategory("orders", GwpText.Get(GwpTextCatalog.gwp_affairs_orders));
                AddCategory("support", GwpText.Get(GwpTextCatalog.gwp_affairs_support));
                if (_entries.Any(e => e.IsPersonal && e.Id.StartsWith("request:"))) AddCategory("requests", GwpText.Get(GwpTextCatalog.gwp_affairs_petitions));
            }
            else if (_page == 1 || _page == 2)
            {
                foreach (var duty in GwpAffairsData.Duties) AddCategory(duty.ToString(), GwpAffairsText.Duty(duty));
                if (_page == 1) AddCategory("children", GwpText.Get(GwpTextCatalog.gwp_affairs_children));
            }
            else if (_page == 3) AddCategory("faith", GwpText.Get(GwpTextCatalog.gwp_affairs_religion));
            SelectCategory(_category);
        }
        private void AddCategory(string id, string label) => Categories.Add(new GwpAffairsChoiceVM(id, label, string.Empty, () => SelectCategory(id)));

        private void SelectCategory(string id)
        {
            _category = Categories.Any(c => c.Id == id) ? id : Categories.FirstOrDefault()?.Id ?? string.Empty;
            foreach (var oldRow in Rows) oldRow.OnFinalize();
            Rows.Clear();
            if (_page == 1) _visible = MemberEntries();
            else if (_page == 3) _visible = _faith;
            else if (_page == 4) _visible = new List<GwpAffairsEntry>();
            else _visible = _entries.Where(e => _page == 2
                ? e.Duty.ToString() == _category && !e.Id.StartsWith("dispatch:")
                : _category switch
                {
                    "current" => e.Id == "commission",
                    "couriers" => e.Id.StartsWith("dispatch:"),
                    "orders" => e.Id.StartsWith("order:"),
                    "support" => e.Id.StartsWith("mediation:") || e.Id.StartsWith("support:"),
                    "requests" => e.Id.StartsWith("request:"),
                    _ => false
                }).ToList();
            // Available cases rank by estimated personal fee, then lighter target strength, with stable IDs.
            if (_page == 2 && (_category == "CaravanProtection" || _category == "VillageProtection"))
                _visible = _visible.OrderByDescending(e => e.IsOffer).ThenByDescending(e =>
                    CrimePool.GetRecordByKey(e.Id) is CrimeRecord crime ? PlayerBountyBehavior.EstimateAffairsReward(crime) : 0)
                    .ThenBy(e => CrimePool.GetRecordByKey(e.Id) is CrimeRecord crime ? GwpStrength.CaseTarget(crime) : float.MaxValue)
                    .ThenBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (GwpAffairsEntry entry in _visible)
            {
                var captured = entry;
                Rows.Add(new GwpAffairsChoiceVM(entry.Id, entry.Title, entry.Group == ListTitle ? entry.Summary : GwpAffairsText.Pair(entry.Group, entry.Summary),
                    () => SelectEntry(captured.Id), _page == 1 ? entry.Hero : null));
            }
            foreach (var tab in Tabs) tab.IsSelected = tab.Id == _page.ToString();
            foreach (var category in Categories) category.IsSelected = category.Id == _category;
            SelectEntry(_selections.TryGetValue(SelectionKey, out string selected) ? selected : _visible.FirstOrDefault()?.Id ?? string.Empty);
        }

        private List<GwpAffairsEntry> MemberEntries()
        {
            var list = new List<GwpAffairsEntry>();
            Clan? clan = PoliceStats.GetPoliceClan();
            if (clan == null) return list;
            foreach (Hero hero in clan.Heroes.Where(h => h.IsAlive).OrderBy(h => h.StringId))
            {
                bool adult = hero.Age >= Campaign.Current.Models.AgeModel.HeroComesOfAge;
                GreyWardenFamilyBehavior.TryGetDuty(hero, out var duty);
                if ((_category == "children") != !adult || adult && duty.ToString() != _category) continue;
                var entry = new GwpAffairsEntry(hero.StringId, GwpAffairsText.Duty(duty), hero.Name.ToString())
                { Hero = hero, Place = hero.CurrentSettlement, Duty = duty, Summary = GreyWardenFamilyBehavior.GetDutyTitle(hero),
                    NotesTitle = GwpText.Get(GwpTextCatalog.gwp_affairs_organization) };
                if (hero.PartyBelongedTo != null)
                {
                    entry.Place = GwpCommon.NormalizePlayerFacingSettlement(hero.CurrentSettlement, hero.PartyBelongedTo.GetPosition2D);
                    entry.Fields.Add(GwpAffairsText.TargetLocation(hero.PartyBelongedTo, GwpTextCatalog.gwp_affairs_current_location));
                    entry.Fields.Add(hero.PartyBelongedTo.GetBehaviorText().ToString());
                }
                foreach (var task in _entries.Where(e => e.Assignee == hero))
                {
                    var lines = task.IsPaused ? entry.Pending : entry.Work;
                    lines.Add(task.Title);
                    lines.Add(task.WorkSummary);
                }
                foreach (var report in PoliceEnforcementBehavior.GetEnvoyReports().Where(r => r.WardenHeroId == hero.StringId))
                {
                    var formatted = new GwpCaseArchiveItemVM(report);
                    entry.Reports.Add(GwpAffairsText.Pair(formatted.Header, formatted.TimeText));
                    entry.Reports.Add(formatted.DetailsText);
                }
                if (!adult && GreyWardenVillageAdoptionBehavior.TryGetAdoptionOrigin(hero.StringId, out string origin)) entry.Fields.Add(origin);
                entry.Notes.Add(GwpText.Get(GwpTextCatalog.gwp_gwpcasearchivescreen_056, "VAR_1", PoliceResourceManager.GetJudicialTreasuryBalance()));
                foreach (var war in GwpPoliceWarReasonService.GetCurrentWarReasonEntries()) entry.Notes.Add(new GwpCaseArchiveItemVM(war).AssignmentText);
                if (GreyWardenVillageAdoptionBehavior.TryGetAdoptionStatus(out var adoption))
                    entry.Notes.Add(GwpAffairsText.Line(GwpTextCatalog.gwp_affairs_family,
                        GwpAffairsText.Pair(adoption.LivingMembers.ToString(), adoption.MaxMembers.ToString())));
                list.Add(entry);
            }
            return list;
        }

        private void SelectEntry(string id)
        {
            _selected = _visible.FirstOrDefault(e => e.Id == id) ?? _visible.FirstOrDefault();
            if (_selected != null) _selections[SelectionKey] = _selected.Id;
            foreach (var row in Rows) row.IsSelected = row.Id == _selected?.Id;
            Fields.Clear(); Sections.Clear();
            if (_selected != null)
            {
                foreach (string line in _selected.Fields.Where(s => !string.IsNullOrWhiteSpace(s))) Fields.Add(new GwpAffairsLineVM(line));
                if (_selected.Work.Count > 0) Sections.Add(new GwpAffairsSectionVM(GwpText.Get(GwpTextCatalog.gwp_affairs_work), _selected.Work));
                if (_selected.Pending.Count > 0) Sections.Add(new GwpAffairsSectionVM(GwpText.Get(GwpTextCatalog.gwp_affairs_queued_work), _selected.Pending));
                if (_selected.Reports.Count > 0) Sections.Add(new GwpAffairsSectionVM(GwpText.Get(GwpTextCatalog.gwp_affairs_negotiations), _selected.Reports));
                if (_selected.Notes.Count > 0) Sections.Add(new GwpAffairsSectionVM(
                    string.IsNullOrEmpty(_selected.NotesTitle) ? GwpText.Get(GwpTextCatalog.gwp_affairs_processing) : _selected.NotesTitle, _selected.Notes));
            }
            foreach (string property in new[] { nameof(Standing), nameof(ListTitle), nameof(DetailTitle), nameof(DetailSummary), nameof(IsEmpty),
                         nameof(EmptyText), nameof(CanAccept), nameof(ShowAccept), nameof(HasHero), nameof(HasPlace), nameof(Status), nameof(ShowBrowse), nameof(ShowRefresh) })
                OnPropertyChanged(property);
        }
        public void ExecuteAccept()
        {
            if (_selected?.IsOffer != true) return;
            bool accepted = Bounty?.TryAcceptAffairsCase(_selected.Id, _selected.DisplayedFine, _selected.DisplayedReward) == true;
            _status = accepted ? string.Empty : GwpText.Get(GwpTextCatalog.gwp_affairs_refresh_changed);
            if (accepted) { _page = 0; _category = "current"; }
            Refresh();
        }
        public void ExecuteHero()
        {
            Hero? hero = _selected?.Hero ?? _selected?.Assignee;
            if (hero == null) return;
            Campaign.Current.EncyclopediaManager.GoToLink(hero.EncyclopediaLink);
        }
        public void ExecutePlace()
        {
            var place = _selected?.Place;
            if (place == null) return;
            Campaign.Current.EncyclopediaManager.GoToLink(place.EncyclopediaLink);
        }
        public override void OnFinalize()
        {
            foreach (var row in Rows) row.OnFinalize();
            DoneInputKey?.OnFinalize();
            base.OnFinalize();
        }
    }
}
