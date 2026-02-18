using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services; // For TimeFormatHelper

namespace WPF_LoginForm.ViewModels
{
    // --- Helper Classes ---
    public class CheckableItem : ViewModelBase
    {
        private bool _isChecked;
        public string Name { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
    }

    public class FilterChip : ViewModelBase
    {
        public string Label { get; set; }
        public string FilterType { get; set; } // "Machine", "Reason"
        public string Value { get; set; }
    }

    // --- Main ViewModel ---
    public class ErrorDrillDownViewModel : ViewModelBase
    {
        private readonly List<ErrorLogItem> _allItemsSource;
        private readonly List<string> _excludedMachines; // Machines to HIDE if in "Others" mode
        private readonly bool _useClockFormat;
        private readonly bool _excludeMachine00;

        private string _windowTitle;
        public string WindowTitle { get => _windowTitle; set => SetProperty(ref _windowTitle, value); }

        public ObservableCollection<ErrorLogItem> DisplayedItems { get; set; }
        public ObservableCollection<CheckableItem> MachineFilterList { get; set; }
        public ObservableCollection<FilterChip> ActiveFilters { get; set; }

        public int RecordCount => DisplayedItems?.Count ?? 0;

        // Dynamic Total Duration using Helper
        public string TotalDurationText => GetTotalDuration();

        // Commands
        public ICommand ApplyFilterCommand { get; private set; }

        public ICommand RemoveFilterChipCommand { get; private set; }

        // Constructor
        public ErrorDrillDownViewModel(IEnumerable<ErrorLogItem> items, string title, string initialFilter, List<string> excludedMachines, bool useClockFormat, bool excludeMachine00)
        {
            WindowTitle = title;
            _allItemsSource = items.ToList();
            _excludedMachines = excludedMachines ?? new List<string>();
            _useClockFormat = useClockFormat;
            _excludeMachine00 = excludeMachine00;

            ActiveFilters = new ObservableCollection<FilterChip>();

            // 1. Format items immediately based on the setting passed from MainViewModel
            foreach (var item in _allItemsSource)
            {
                item.DisplayDuration = TimeFormatHelper.FormatDuration(item.DurationMinutes, _useClockFormat);
            }

            // 2. Initialize Machine Combobox
            // Filter out MA-00 if global exclude is on
            var uniqueMachines = _allItemsSource
                .Select(x => x.MachineCode)
                .Distinct()
                .Where(m => !_excludeMachine00 || (m != "MA-00" && m != "MA-0"))
                .OrderBy(m => m)
                .Select(m => new CheckableItem { Name = m, IsChecked = false })
                .ToList();

            MachineFilterList = new ObservableCollection<CheckableItem>(uniqueMachines);

            ApplyFilterCommand = new ViewModelCommand(ExecuteApplyComboFilter);
            RemoveFilterChipCommand = new ViewModelCommand(ExecuteRemoveFilterChip);

            // 3. Apply Initial Filter (Handles "Others" logic)
            ApplyInitialFilter(initialFilter);
        }

        private void ApplyInitialFilter(string filterText)
        {
            // Base Query
            var baseQuery = _allItemsSource.AsEnumerable();
            if (_excludeMachine00)
            {
                baseQuery = baseQuery.Where(x => x.MachineCode != "MA-00" && x.MachineCode != "MA-0");
            }

            if (string.IsNullOrEmpty(filterText))
            {
                // No filter, just show base query
            }
            else if (filterText == "MACHINE_OTHERS")
            {
                // TASK 1: Visual Selection Logic for "Others"
                foreach (var m in MachineFilterList)
                {
                    // If this machine is NOT in the excluded (Top 9) list, check it.
                    if (!_excludedMachines.Contains(m.Name))
                    {
                        m.IsChecked = true;
                        AddChip("Machine", m.Name); // Add visual chips too
                    }
                }

                // Filter data based on "Not in Top 9"
                if (_excludedMachines.Any())
                {
                    baseQuery = baseQuery.Where(x => !_excludedMachines.Contains(x.MachineCode));
                }
            }
            else if (filterText.StartsWith("MA-"))
            {
                // Specific Machine Clicked
                AddChip("Machine", filterText);
                var item = MachineFilterList.FirstOrDefault(m => m.Name == filterText);
                if (item != null) item.IsChecked = true;

                baseQuery = baseQuery.Where(x => x.MachineCode == filterText);
            }
            else
            {
                // Reason Clicked
                AddChip("Reason", filterText);
                baseQuery = baseQuery.Where(x =>
                    !string.IsNullOrEmpty(x.ErrorMessage) &&
                    x.ErrorMessage.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // Execute Query
            DisplayedItems = new ObservableCollection<ErrorLogItem>(baseQuery);

            OnPropertyChanged(nameof(DisplayedItems));
            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalDurationText));
        }

        private void RefreshData()
        {
            var query = _allItemsSource.AsEnumerable();

            // Always apply global exclusion first
            if (_excludeMachine00)
            {
                query = query.Where(x => x.MachineCode != "MA-00" && x.MachineCode != "MA-0");
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
                    !string.IsNullOrEmpty(x.ErrorMessage) &&
                    x.ErrorMessage.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            DisplayedItems = new ObservableCollection<ErrorLogItem>(query);

            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalDurationText));
        }

        private void ExecuteApplyComboFilter(object obj)
        {
            // Clear existing machine chips to rebuild from checkboxes
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