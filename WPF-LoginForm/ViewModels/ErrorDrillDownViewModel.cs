using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_LoginForm.Models; // Ensure you have this namespace

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

    public class FilterChip : ViewModelBase
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

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public ObservableCollection<ErrorLogItem> DisplayedItems
        {
            get => _displayedItems;
            set
            {
                if (SetProperty(ref _displayedItems, value))
                {
                    OnPropertyChanged("RecordCount");
                    OnPropertyChanged("TotalDurationText");
                }
            }
        }

        public ObservableCollection<CheckableItem> MachineFilterList
        {
            get => _machineFilterList;
            set => SetProperty(ref _machineFilterList, value);
        }

        public ObservableCollection<FilterChip> ActiveFilters
        {
            get => _activeFilters;
            set => SetProperty(ref _activeFilters, value);
        }

        public int RecordCount => DisplayedItems?.Count ?? 0;
        public string TotalDurationText => string.Format("{0} min", DisplayedItems?.Sum(x => x.DurationMinutes) ?? 0);

        // Commands
        public ICommand CloseCommand { get; private set; }

        public ICommand ApplyFilterCommand { get; private set; }
        public ICommand RemoveFilterChipCommand { get; private set; }

        public ErrorDrillDownViewModel(IEnumerable<ErrorLogItem> items, string title, string initialFilter)
        {
            WindowTitle = title;
            _allItemsSource = items.ToList();
            ActiveFilters = new ObservableCollection<FilterChip>();
            DisplayedItems = new ObservableCollection<ErrorLogItem>(_allItemsSource);

            // 1. Initialize Machine Combo List
            var uniqueMachines = _allItemsSource
                .Where(x => !string.IsNullOrEmpty(x.MachineCode))
                .Select(x => x.MachineCode)
                .Distinct()
                .OrderBy(m => m)
                .Select(m => new CheckableItem { Name = m, IsChecked = false })
                .ToList();

            MachineFilterList = new ObservableCollection<CheckableItem>(uniqueMachines);

            // 2. Initialize Commands
            ApplyFilterCommand = new ViewModelCommand(ExecuteApplyComboFilter);
            RemoveFilterChipCommand = new ViewModelCommand(ExecuteRemoveFilterChip);
            // CloseCommand needs to be handled by the View's CodeBehind or a WindowService usually,
            // but for MVVM purity usually we use an event or Action.
            // The XAML uses "Click" handlers, so we can leave this null here if using Click events.

            // 3. Apply Initial Filter
            if (!string.IsNullOrEmpty(initialFilter))
            {
                AddInitialFilter(initialFilter);
            }
        }

        private void AddInitialFilter(string filterText)
        {
            // Logic to handle "MA-01-Reason" or just "MA-01"
            string machineName = null;
            string reasonName = null;

            if (filterText.StartsWith("MA-"))
            {
                var parts = filterText.Split('-');
                // Assuming format MA-01 (parts[0]-parts[1])
                if (parts.Length >= 2)
                {
                    machineName = parts[0] + "-" + parts[1];
                    AddChip("Machine", machineName);

                    // Sync with the ComboBox
                    var item = MachineFilterList.FirstOrDefault(m => m.Name == machineName);
                    if (item != null) item.IsChecked = true;
                }

                if (parts.Length > 2)
                {
                    reasonName = string.Join("-", parts.Skip(2));
                    AddChip("Reason", reasonName);
                }
            }
            else
            {
                // Pure Reason filter
                AddChip("Reason", filterText);
            }

            RefreshData();
        }

        private void AddChip(string type, string value)
        {
            // Prevent duplicates
            if (!ActiveFilters.Any(x => x.Value == value && x.FilterType == type))
            {
                ActiveFilters.Add(new FilterChip { Label = value, FilterType = type, Value = value });
            }
        }

        private void ExecuteApplyComboFilter(object obj)
        {
            // 1. Remove existing "Machine" chips (we are refreshing the machine selection)
            var machinesToRemove = ActiveFilters.Where(x => x.FilterType == "Machine").ToList();
            foreach (var chip in machinesToRemove)
            {
                ActiveFilters.Remove(chip);
            }

            // 2. Add chips for every Checked item in the ComboBox
            foreach (var m in MachineFilterList.Where(x => x.IsChecked))
            {
                AddChip("Machine", m.Name);
            }

            RefreshData();
        }

        private void ExecuteRemoveFilterChip(object obj)
        {
            var chip = obj as FilterChip;
            if (chip != null)
            {
                ActiveFilters.Remove(chip);

                // If it was a machine, uncheck it in the ComboBox
                if (chip.FilterType == "Machine")
                {
                    var comboItem = MachineFilterList.FirstOrDefault(m => m.Name == chip.Value);
                    if (comboItem != null) comboItem.IsChecked = false;
                }

                RefreshData();
            }
        }

        private void RefreshData()
        {
            IEnumerable<ErrorLogItem> query = _allItemsSource;

            // 1. Machine Filters
            var machineFilters = ActiveFilters
                .Where(x => x.FilterType == "Machine")
                .Select(x => x.Value)
                .ToList();

            if (machineFilters.Any())
            {
                // Show record if it matches ANY of the selected machines
                query = query.Where(x => machineFilters.Contains(x.MachineCode));
            }

            // 2. Reason Filters
            var reasonFilters = ActiveFilters
                .Where(x => x.FilterType == "Reason")
                .Select(x => x.Value)
                .ToList();

            if (reasonFilters.Any())
            {
                // Show record if it matches ANY of the selected reasons (Partial text match)
                query = query.Where(x => reasonFilters.Any(r =>
                    !string.IsNullOrEmpty(x.ErrorMessage) &&
                    x.ErrorMessage.IndexOf(r, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            DisplayedItems = new ObservableCollection<ErrorLogItem>(query);
        }
    }
}