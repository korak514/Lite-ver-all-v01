// ViewModels/ErrorDrillDownViewModel.cs
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
        public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
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

        private string _windowTitle;
        public string WindowTitle { get => _windowTitle; set => SetProperty(ref _windowTitle, value); }

        public ObservableCollection<ErrorEventModel> DisplayedItems { get; set; }
        public ObservableCollection<CheckableItem> MachineFilterList { get; set; }
        public ObservableCollection<FilterChip> ActiveFilters { get; set; }

        public int RecordCount => DisplayedItems?.Count ?? 0;
        public string TotalDurationText => GetTotalDuration();

        public ICommand ApplyFilterCommand { get; private set; }
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

            var uniqueMachines = _allItemsSource
                .Select(x => x.MachineCode)
                .Distinct()
                .Where(m => !_excludeMachine00 || (m != "00" && m != "0" && m != "MA-00" && m != "MA-0"))
                .OrderBy(m => m)
                .Select(m => new CheckableItem { Name = m, IsChecked = false })
                .ToList();

            MachineFilterList = new ObservableCollection<CheckableItem>(uniqueMachines);

            ApplyFilterCommand = new ViewModelCommand(ExecuteApplyComboFilter);
            RemoveFilterChipCommand = new ViewModelCommand(ExecuteRemoveFilterChip);

            ApplyInitialFilter(initialFilter);
        }

        private void ApplyInitialFilter(string filterText)
        {
            var baseQuery = _allItemsSource.AsEnumerable();
            if (_excludeMachine00)
            {
                baseQuery = baseQuery.Where(x => x.MachineCode != "00" && x.MachineCode != "0" && x.MachineCode != "MA-00" && x.MachineCode != "MA-0");
            }

            if (string.IsNullOrEmpty(filterText))
            {
                // No filter
            }
            else if (filterText.StartsWith("MACHINE_OTHERS"))
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
                    {
                        m.IsChecked = true;
                        AddChip("Machine", m.Name);
                    }
                }

                if (_excludedMachines.Any())
                {
                    baseQuery = baseQuery.Where(x => !_excludedMachines.Contains(x.MachineCode));
                }
            }
            else if (filterText.StartsWith("MACHINE_CATEGORY|"))
            {
                var parts = filterText.Split('|');
                if (parts.Length == 3)
                {
                    string machine = parts[1];
                    string category = parts[2];

                    AddChip("Machine", machine);
                    var item = MachineFilterList.FirstOrDefault(m => m.Name == machine);
                    if (item != null) item.IsChecked = true;

                    AddChip("Reason", category);

                    baseQuery = baseQuery.Where(x =>
                        x.MachineCode == machine &&
                        !string.IsNullOrEmpty(x.ErrorDescription) &&
                        _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules).Equals(category, StringComparison.OrdinalIgnoreCase));
                }
            }
            else if (filterText.StartsWith("MA-"))
            {
                string rawMachine = filterText.Replace("MA-", "");
                AddChip("Machine", rawMachine);
                var item = MachineFilterList.FirstOrDefault(m => m.Name == rawMachine);
                if (item != null) item.IsChecked = true;

                baseQuery = baseQuery.Where(x => x.MachineCode == rawMachine);
            }
            else
            {
                AddChip("Reason", filterText);
                baseQuery = baseQuery.Where(x =>
                    !string.IsNullOrEmpty(x.ErrorDescription) &&
                    _mappingService.GetMappedCategory(x.ErrorDescription, _activeRules).Equals(filterText, StringComparison.OrdinalIgnoreCase));
            }

            DisplayedItems = new ObservableCollection<ErrorEventModel>(baseQuery);

            OnPropertyChanged(nameof(DisplayedItems));
            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalDurationText));
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

            DisplayedItems = new ObservableCollection<ErrorEventModel>(query);

            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalDurationText));
        }

        private void ExecuteApplyComboFilter(object obj)
        {
            var remove = ActiveFilters.Where(x => x.FilterType == "Machine").ToList();
            foreach (var r in remove) ActiveFilters.Remove(r);

            foreach (var m in MachineFilterList.Where(x => x.IsChecked))
            {
                AddChip("Machine", m.Name);
            }
            RefreshData();
        }

        private void ExecuteRemoveFilterChip(object obj)
        {
            if (obj is FilterChip chip)
            {
                ActiveFilters.Remove(chip);
                if (chip.FilterType == "Machine")
                {
                    var item = MachineFilterList.FirstOrDefault(m => m.Name == chip.Value);
                    if (item != null) item.IsChecked = false;
                }
                RefreshData();
            }
        }

        private void AddChip(string type, string value)
        {
            if (!ActiveFilters.Any(x => x.Value == value && x.FilterType == type))
                ActiveFilters.Add(new FilterChip { Label = value, FilterType = type, Value = value });
        }

        private string GetTotalDuration()
        {
            double total = DisplayedItems?.Sum(x => x.DurationMinutes) ?? 0;
            return TimeFormatHelper.FormatDuration(total, _useClockFormat);
        }
    }
}