using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class CheckableItem : ViewModelBase
    {
        private bool _isChecked;
        public string Name { get; set; }
        public string Type { get; set; } // "Machine" or "Reason"
        public Action OnCheckChanged { get; set; } // Callback to trigger filter immediately

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetProperty(ref _isChecked, value))
                {
                    OnCheckChanged?.Invoke(); // Fire immediately when property changes
                }
            }
        }
    }

    public class FilterChip : ViewModelBase
    {
        public string Label { get; set; }
        public string FilterType { get; set; }
        public string Value { get; set; }
    }

    public class ErrorDrillDownViewModel : ViewModelBase
    {
        private readonly List<ErrorEventModel> _allItemsSource;
        private readonly List<string> _excludedMachines;
        private readonly bool _useClockFormat;
        private readonly bool _excludeMachine00;

        private readonly CategoryMappingService _mappingService;
        private readonly List<CategoryRule> _activeRules;
        private bool _isUpdatingFilters = false; // Prevents recursive loops

        private string _windowTitle;
        public string WindowTitle { get => _windowTitle; set => SetProperty(ref _windowTitle, value); }

        // FIX: Properly encapsulated so the DataGrid updates instantly when the collection is swapped.
        private ObservableCollection<ErrorEventModel> _displayedItems;

        public ObservableCollection<ErrorEventModel> DisplayedItems
        {
            get => _displayedItems;
            set => SetProperty(ref _displayedItems, value);
        }

        public ObservableCollection<FilterChip> ActiveFilters { get; set; }

        // Master Lists
        public ObservableCollection<CheckableItem> MachineFilterList { get; set; }

        public ObservableCollection<CheckableItem> ReasonFilterList { get; set; }

        // Filtered Lists (Bound to UI)
        private ObservableCollection<CheckableItem> _filteredMachineList;

        public ObservableCollection<CheckableItem> FilteredMachineList { get => _filteredMachineList; set => SetProperty(ref _filteredMachineList, value); }

        private ObservableCollection<CheckableItem> _filteredReasonList;
        public ObservableCollection<CheckableItem> FilteredReasonList { get => _filteredReasonList; set => SetProperty(ref _filteredReasonList, value); }

        // UI State
        private bool _isFiltersExpanded = false;

        public bool IsFiltersExpanded { get => _isFiltersExpanded; set => SetProperty(ref _isFiltersExpanded, value); }

        // Search Texts
        private string _machineSearchText;

        public string MachineSearchText
        {
            get => _machineSearchText;
            set
            {
                if (SetProperty(ref _machineSearchText, value))
                {
                    if (!string.IsNullOrWhiteSpace(value)) IsFiltersExpanded = true;
                    FilterMachines();
                }
            }
        }

        private string _reasonSearchText;

        public string ReasonSearchText
        {
            get => _reasonSearchText;
            set
            {
                if (SetProperty(ref _reasonSearchText, value))
                {
                    if (!string.IsNullOrWhiteSpace(value)) IsFiltersExpanded = true;
                    FilterReasons();
                }
            }
        }

        public int RecordCount => DisplayedItems?.Count ?? 0;
        public string TotalDurationText => GetTotalDuration();

        public ICommand RemoveFilterChipCommand { get; private set; }

        public ErrorDrillDownViewModel(IEnumerable<ErrorEventModel> items, string title, string initialFilter, List<string> excludedMachines, bool useClockFormat, bool excludeMachine00)
        {
            WindowTitle = title;
            _allItemsSource = items.ToList();
            _excludedMachines = excludedMachines ?? new List<string>();
            _useClockFormat = useClockFormat;
            _excludeMachine00 = excludeMachine00;

            _mappingService = new CategoryMappingService();
            _activeRules = _mappingService.LoadRules();

            ActiveFilters = new ObservableCollection<FilterChip>();

            foreach (var item in _allItemsSource)
            {
                item.DisplayDuration = TimeFormatHelper.FormatDuration(item.DurationMinutes, _useClockFormat);
            }

            // Populate Machines
            var uniqueMachines = _allItemsSource
                .Select(x => x.MachineCode)
                .Distinct()
                .Where(m => !_excludeMachine00 || (m != "00" && m != "0" && m != "MA-00" && m != "MA-0"))
                .OrderBy(m => m)
                .Select(m => new CheckableItem { Name = m, Type = "Machine", IsChecked = false, OnCheckChanged = TriggerFilterUpdate })
                .ToList();

            MachineFilterList = new ObservableCollection<CheckableItem>(uniqueMachines);

            // Populate Reasons (Categories)
            var uniqueReasons = _allItemsSource
                .Where(x => !string.IsNullOrEmpty(x.ErrorDescription))
                .Select(x => _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules))
                .Distinct()
                .OrderBy(r => r)
                .Select(r => new CheckableItem { Name = r, Type = "Reason", IsChecked = false, OnCheckChanged = TriggerFilterUpdate })
                .ToList();

            ReasonFilterList = new ObservableCollection<CheckableItem>(uniqueReasons);

            RemoveFilterChipCommand = new ViewModelCommand(ExecuteRemoveFilterChip);

            ApplyInitialFilter(initialFilter);

            // Initialize filtered lists after initial filter is applied
            FilteredMachineList = new ObservableCollection<CheckableItem>(MachineFilterList);
            FilteredReasonList = new ObservableCollection<CheckableItem>(ReasonFilterList);
        }

        private void FilterMachines()
        {
            if (string.IsNullOrWhiteSpace(MachineSearchText))
                FilteredMachineList = new ObservableCollection<CheckableItem>(MachineFilterList);
            else
                FilteredMachineList = new ObservableCollection<CheckableItem>(
                    MachineFilterList.Where(m => m.Name.IndexOf(MachineSearchText, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void FilterReasons()
        {
            if (string.IsNullOrWhiteSpace(ReasonSearchText))
                FilteredReasonList = new ObservableCollection<CheckableItem>(ReasonFilterList);
            else
                FilteredReasonList = new ObservableCollection<CheckableItem>(
                    ReasonFilterList.Where(r => r.Name.IndexOf(ReasonSearchText, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private void ApplyInitialFilter(string filterText)
        {
            _isUpdatingFilters = true;

            if (!string.IsNullOrEmpty(filterText))
            {
                if (filterText.StartsWith("MACHINE_OTHERS"))
                {
                    var parts = filterText.Split('|');
                    if (parts.Length > 1)
                    {
                        _excludedMachines.Clear();
                        _excludedMachines.AddRange(parts[1].Split(','));
                    }

                    foreach (var m in MachineFilterList)
                    {
                        if (!_excludedMachines.Contains(m.Name))
                            m.IsChecked = true;
                    }
                }
                else if (filterText.StartsWith("MACHINE_CATEGORY|"))
                {
                    var parts = filterText.Split('|');
                    if (parts.Length == 3)
                    {
                        string machine = parts[1];
                        string category = parts[2];

                        var mItem = MachineFilterList.FirstOrDefault(m => m.Name == machine);
                        if (mItem != null) mItem.IsChecked = true;

                        var rItem = ReasonFilterList.FirstOrDefault(r => r.Name == category);
                        if (rItem != null) rItem.IsChecked = true;
                    }
                }
                else if (filterText.StartsWith("MA-"))
                {
                    string rawMachine = filterText.Replace("MA-", "");
                    var item = MachineFilterList.FirstOrDefault(m => m.Name == rawMachine);
                    if (item != null) item.IsChecked = true;
                }
                else
                {
                    var item = ReasonFilterList.FirstOrDefault(r => r.Name == filterText);
                    if (item != null) item.IsChecked = true;
                }
            }

            _isUpdatingFilters = false;
            TriggerFilterUpdate();
        }

        // Rebuilds chips and data grid based on current checkbox states
        private void TriggerFilterUpdate()
        {
            if (_isUpdatingFilters) return;
            _isUpdatingFilters = true;

            ActiveFilters.Clear();

            foreach (var m in MachineFilterList.Where(x => x.IsChecked))
                ActiveFilters.Add(new FilterChip { Label = m.Name, FilterType = "Machine", Value = m.Name });

            foreach (var r in ReasonFilterList.Where(x => x.IsChecked))
                ActiveFilters.Add(new FilterChip { Label = r.Name, FilterType = "Reason", Value = r.Name });

            RefreshData();
            _isUpdatingFilters = false;
        }

        private void RefreshData()
        {
            var query = _allItemsSource.AsEnumerable();

            if (_excludeMachine00)
            {
                query = query.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00" && x.MachineCode != "MA-0");
            }

            var machineFilters = ActiveFilters.Where(x => x.FilterType == "Machine").Select(x => x.Value).ToList();
            var reasonFilters = ActiveFilters.Where(x => x.FilterType == "Reason").Select(x => x.Value).ToList();

            if (machineFilters.Any())
            {
                query = query.Where(x => machineFilters.Contains(x.MachineCode));
            }

            if (reasonFilters.Any())
            {
                query = query.Where(x => reasonFilters.Any(r =>
                    !string.IsNullOrEmpty(x.ErrorDescription) &&
                    _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules).Equals(r, StringComparison.OrdinalIgnoreCase)));
            }

            // Assigning this will now trigger the UI update thanks to the SetProperty fix!
            DisplayedItems = new ObservableCollection<ErrorEventModel>(query);

            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalDurationText));
        }

        private void ExecuteRemoveFilterChip(object obj)
        {
            if (obj is FilterChip chip)
            {
                _isUpdatingFilters = true; // Prevent recursive update while unchecking

                ActiveFilters.Remove(chip);

                if (chip.FilterType == "Machine")
                {
                    var item = MachineFilterList.FirstOrDefault(m => m.Name == chip.Value);
                    if (item != null) item.IsChecked = false;
                }
                else if (chip.FilterType == "Reason")
                {
                    var item = ReasonFilterList.FirstOrDefault(r => r.Name == chip.Value);
                    if (item != null) item.IsChecked = false;
                }

                _isUpdatingFilters = false;
                TriggerFilterUpdate(); // Rebuild cleanly
            }
        }

        private string GetTotalDuration()
        {
            double total = DisplayedItems?.Sum(x => x.DurationMinutes) ?? 0;
            return TimeFormatHelper.FormatDuration(total, _useClockFormat);
        }
    }
}