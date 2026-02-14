using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
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

    public class FilterChip
    {
        public string Label { get; set; }
        public string FilterType { get; set; } // "Machine" or "Reason"
        public string Value { get; set; }
    }

    public class ErrorDrillDownViewModel : ViewModelBase
    {
        private readonly List<ErrorLogItem> _allItemsSource;
        private string _windowTitle;
        private ObservableCollection<ErrorLogItem> _displayedItems;
        private ObservableCollection<CheckableItem> _machineFilterList;
        private ObservableCollection<FilterChip> _activeFilters;

        public string WindowTitle { get => _windowTitle; set => SetProperty(ref _windowTitle, value); }

        public ObservableCollection<ErrorLogItem> DisplayedItems
        {
            get => _displayedItems;
            set
            {
                if (SetProperty(ref _displayedItems, value))
                {
                    OnPropertyChanged(nameof(RecordCount));
                    OnPropertyChanged(nameof(TotalDurationText));
                }
            }
        }

        public ObservableCollection<CheckableItem> MachineFilterList { get => _machineFilterList; set => SetProperty(ref _machineFilterList, value); }

        // This holds the "Boxes with X"
        public ObservableCollection<FilterChip> ActiveFilters { get => _activeFilters; set => SetProperty(ref _activeFilters, value); }

        public int RecordCount => DisplayedItems?.Count ?? 0;
        public string TotalDurationText => $"{DisplayedItems?.Sum(x => x.DurationMinutes) ?? 0} min";

        public ICommand CloseCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand RemoveFilterChipCommand { get; }

        public ErrorDrillDownViewModel(IEnumerable<ErrorLogItem> items, string title, string initialFilter)
        {
            WindowTitle = title;
            _allItemsSource = items.ToList();
            ActiveFilters = new ObservableCollection<FilterChip>();
            DisplayedItems = new ObservableCollection<ErrorLogItem>(_allItemsSource);

            // 1. Initialize Machine Combo List
            var uniqueMachines = _allItemsSource
                .Select(x => x.MachineCode)
                .Distinct().OrderBy(m => m)
                .Select(m => new CheckableItem { Name = m, IsChecked = false })
                .ToList();
            MachineFilterList = new ObservableCollection<CheckableItem>(uniqueMachines);

            // 2. Commands
            ApplyFilterCommand = new ViewModelCommand(ExecuteApplyComboFilter);
            RemoveFilterChipCommand = new ViewModelCommand(ExecuteRemoveFilterChip);

            // 3. Apply Initial Filter (from Chart Click) as a Chip
            if (!string.IsNullOrEmpty(initialFilter))
            {
                AddInitialFilter(initialFilter);
            }
        }

        private void AddInitialFilter(string filterText)
        {
            // Determine if it's a Machine or Reason based on format
            // Chart click format might be "MA-01" or "Electrical" or "MA-01-Electrical"

            if (filterText.StartsWith("MA-"))
            {
                var parts = filterText.Split('-'); // MA, 01, Reason

                // Add Machine Chip
                string machine = parts[0] + "-" + parts[1]; // MA-01
                ActiveFilters.Add(new FilterChip { Label = machine, FilterType = "Machine", Value = machine });

                // If combined (MA-01-Reason), add Reason Chip too
                if (parts.Length > 2)
                {
                    string reason = string.Join("-", parts.Skip(2));
                    ActiveFilters.Add(new FilterChip { Label = reason, FilterType = "Reason", Value = reason });
                }
            }
            else
            {
                // Pure Reason filter
                ActiveFilters.Add(new FilterChip { Label = filterText, FilterType = "Reason", Value = filterText });
            }

            RefreshData();
        }

        private void ExecuteApplyComboFilter(object obj)
        {
            // When user checks boxes in combo and clicks Apply
            // We treat these as a specific "ComboMachine" filter type

            // 1. Remove existing Combo filters to replace them
            var existingComboFilters = ActiveFilters.Where(x => x.FilterType == "ComboMachine").ToList();
            foreach (var f in existingComboFilters) ActiveFilters.Remove(f);

            // 2. Add new ones
            foreach (var m in MachineFilterList.Where(x => x.IsChecked))
            {
                ActiveFilters.Add(new FilterChip { Label = m.Name, FilterType = "ComboMachine", Value = m.Name });
            }

            RefreshData();
        }

        private void ExecuteRemoveFilterChip(object obj)
        {
            if (obj is FilterChip chip)
            {
                ActiveFilters.Remove(chip);

                // If it was a machine from the combo box, uncheck it in the UI
                var comboItem = MachineFilterList.FirstOrDefault(m => m.Name == chip.Value);
                if (comboItem != null) comboItem.IsChecked = false;

                RefreshData();
            }
        }

        private void RefreshData()
        {
            IEnumerable<ErrorLogItem> query = _allItemsSource;

            // 1. Machine Filters (Combine Initial Machine + Combo Machines)
            var machineFilters = ActiveFilters.Where(x => x.FilterType == "Machine" || x.FilterType == "ComboMachine").Select(x => x.Value).ToList();
            if (machineFilters.Any())
            {
                query = query.Where(x => machineFilters.Contains(x.MachineCode));
            }

            // 2. Reason Filters
            var reasonFilters = ActiveFilters.Where(x => x.FilterType == "Reason").Select(x => x.Value).ToList();
            if (reasonFilters.Any())
            {
                // Case insensitive partial match
                query = query.Where(x => reasonFilters.Any(r => x.ErrorMessage != null && x.ErrorMessage.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            DisplayedItems = new ObservableCollection<ErrorLogItem>(query);
        }
    }
}