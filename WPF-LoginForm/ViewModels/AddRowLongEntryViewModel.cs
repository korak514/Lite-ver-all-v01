using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using WPF_LoginForm.Repositories; // For IDataRepository
using WPF_LoginForm.Services;   // For ILogger (optional here, parent VM can log)

namespace WPF_LoginForm.ViewModels
{
    public class AddRowLongEntryViewModel : ViewModelBase
    {
        private readonly string _owningTableName;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger; // Optional, for debugging this specific VM

        // --- Part Selections ---
        private string _selectedPart1;
        public string SelectedPart1
        {
            get => _selectedPart1;
            set
            {
                if (SetProperty(ref _selectedPart1, value))
                {
                    // When Part1 changes, clear subsequent parts and their options, then load Part2 options
                    SelectedPart2 = null; // This will trigger its own chain reaction if not null
                    Part2Options.Clear();
                    Part3Options.Clear();
                    Part4Options.Clear();
                    CoreItemOptions.Clear();
                    DisplayCoreItem = string.Empty;
                    ActualTargetColumnName = null;
                    if (!string.IsNullOrEmpty(_selectedPart1))
                    {
                        LoadPart2OptionsAsync();
                    }
                    UpdateVisibilities();
                }
            }
        }

        private string _selectedPart2;
        public string SelectedPart2
        {
            get => _selectedPart2;
            set
            {
                if (SetProperty(ref _selectedPart2, value))
                {
                    SelectedPart3 = null;
                    Part3Options.Clear();
                    Part4Options.Clear();
                    CoreItemOptions.Clear();
                    DisplayCoreItem = string.Empty;
                    ActualTargetColumnName = null;
                    if (!string.IsNullOrEmpty(_selectedPart1) && _selectedPart2 != null) // Part2 can be legitimately NULL if hierarchy skips it
                    {
                        LoadPart3OptionsAsync();
                    }
                    else if (string.IsNullOrEmpty(_selectedPart2) && !string.IsNullOrEmpty(_selectedPart1)) // Part2 cleared or explicitly set to represent "no Part2"
                    {
                        LoadCoreItemOptionsAsync(); // Try to load CoreItems based on Part1 only
                    }
                    UpdateVisibilities();
                }
            }
        }

        private string _selectedPart3;
        public string SelectedPart3
        {
            get => _selectedPart3;
            set
            {
                if (SetProperty(ref _selectedPart3, value))
                {
                    SelectedPart4 = null;
                    Part4Options.Clear();
                    CoreItemOptions.Clear();
                    DisplayCoreItem = string.Empty;
                    ActualTargetColumnName = null;
                    if (!string.IsNullOrEmpty(_selectedPart1) && _selectedPart3 != null) // Assumes Part2 would be set if Part3 is
                    {
                        LoadPart4OptionsAsync();
                    }
                    else if (string.IsNullOrEmpty(_selectedPart3) && !string.IsNullOrEmpty(_selectedPart1))// Part3 cleared
                    {
                        LoadCoreItemOptionsAsync(); // Try to load CoreItems based on Part1 & Part2
                    }
                    UpdateVisibilities();
                }
            }
        }

        private string _selectedPart4;
        public string SelectedPart4
        {
            get => _selectedPart4;
            set
            {
                if (SetProperty(ref _selectedPart4, value))
                {
                    CoreItemOptions.Clear();
                    DisplayCoreItem = string.Empty;
                    ActualTargetColumnName = null;
                    // Always try to load CoreItems after the last part selection changes
                    LoadCoreItemOptionsAsync();
                    UpdateVisibilities();
                }
            }
        }

        private string _selectedCoreItem;
        public string SelectedCoreItem
        {
            get => _selectedCoreItem;
            set
            {
                if (SetProperty(ref _selectedCoreItem, value))
                {
                    DisplayCoreItem = value; // Update display
                    UpdateActualTargetColumnNameAsync();
                }
            }
        }

        // --- Options for ComboBoxes ---
        public ObservableCollection<string> Part1Options { get; private set; }
        public ObservableCollection<string> Part2Options { get; private set; }
        public ObservableCollection<string> Part3Options { get; private set; }
        public ObservableCollection<string> Part4Options { get; private set; }
        public ObservableCollection<string> CoreItemOptions { get; private set; }

        // --- Visibility Control for ComboBoxes ---
        private bool _isPart2Visible;
        public bool IsPart2Visible { get => _isPart2Visible; private set => SetProperty(ref _isPart2Visible, value); }

        private bool _isPart3Visible;
        public bool IsPart3Visible { get => _isPart3Visible; private set => SetProperty(ref _isPart3Visible, value); }

        private bool _isPart4Visible;
        public bool IsPart4Visible { get => _isPart4Visible; private set => SetProperty(ref _isPart4Visible, value); }

        private bool _isCoreItemVisible; // For the CoreItem ComboBox
        public bool IsCoreItemVisible { get => _isCoreItemVisible; private set => SetProperty(ref _isCoreItemVisible, value); }

        private bool _isValueEntryVisible; // For the Value TextBox
        public bool IsValueEntryVisible { get => _isValueEntryVisible; private set => SetProperty(ref _isValueEntryVisible, value); }


        // --- Display and Value ---
        private string _displayCoreItem; // To show the user what they are entering a value for
        public string DisplayCoreItem { get => _displayCoreItem; private set => SetProperty(ref _displayCoreItem, value); }

        private object _enteredValue;
        public object EnteredValue { get => _enteredValue; set => SetProperty(ref _enteredValue, value); }

        public string ActualTargetColumnName { get; private set; }


        public AddRowLongEntryViewModel(string owningTableName, IDataRepository dataRepository, ILogger logger)
        {
            _owningTableName = owningTableName;
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger; // Can be null if not needed

            Part1Options = new ObservableCollection<string>();
            Part2Options = new ObservableCollection<string>();
            Part3Options = new ObservableCollection<string>();
            Part4Options = new ObservableCollection<string>();
            CoreItemOptions = new ObservableCollection<string>();

            // Load initial Part1 options
            LoadPart1OptionsAsync();
            UpdateVisibilities();
        }

        private async void LoadPart1OptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part1 options.");
            Part1Options.Clear();
            var options = await _dataRepository.GetDistinctPart1ValuesAsync(_owningTableName);
            options.Insert(0, "Please Select"); // Add a placeholder
            foreach (var opt in options) Part1Options.Add(opt);

            if (Part1Options.Count == 2) // "Please Select" + one actual option
            {
                SelectedPart1 = Part1Options[1]; // Auto-select if only one real option
            }
            else if (Part1Options.Count <= 1) // Only "Please Select" or empty (error)
            {
                _logger?.LogWarning($"[ARLEVM {_owningTableName}] No Part1 options found.");
            }
        }

        private async void LoadPart2OptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part2 options for P1: {SelectedPart1}.");
            Part2Options.Clear();
            Part3Options.Clear(); // Cascade clear
            Part4Options.Clear();
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(SelectedPart1) || SelectedPart1 == "Please Select")
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart2ValuesAsync(_owningTableName, SelectedPart1);
            if (options.Any())
            {
                Part2Options.Add("Please Select");
                foreach (var opt in options) Part2Options.Add(opt);
                if (Part2Options.Count == 2) SelectedPart2 = Part2Options[1];
            }
            else // No Part2 options, try to load CoreItems directly
            {
                LoadCoreItemOptionsAsync();
            }
            UpdateVisibilities();
        }

        private async void LoadPart3OptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part3 options for P1: {SelectedPart1}, P2: {SelectedPart2}.");
            Part3Options.Clear();
            Part4Options.Clear(); // Cascade clear
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(SelectedPart1) || SelectedPart1 == "Please Select" ||
                string.IsNullOrEmpty(SelectedPart2) || SelectedPart2 == "Please Select") // Check if Part2 is also selected meaningfully
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart3ValuesAsync(_owningTableName, SelectedPart1, SelectedPart2);
            if (options.Any())
            {
                Part3Options.Add("Please Select");
                foreach (var opt in options) Part3Options.Add(opt);
                if (Part3Options.Count == 2) SelectedPart3 = Part3Options[1];
            }
            else
            {
                LoadCoreItemOptionsAsync();
            }
            UpdateVisibilities();
        }

        private async void LoadPart4OptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part4 options for P1: {SelectedPart1}, P2: {SelectedPart2}, P3: {SelectedPart3}.");
            Part4Options.Clear();
            CoreItemOptions.Clear(); // Cascade clear
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(SelectedPart1) || SelectedPart1 == "Please Select" ||
                string.IsNullOrEmpty(SelectedPart2) || SelectedPart2 == "Please Select" ||
                string.IsNullOrEmpty(SelectedPart3) || SelectedPart3 == "Please Select")
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart4ValuesAsync(_owningTableName, SelectedPart1, SelectedPart2, SelectedPart3);
            if (options.Any())
            {
                Part4Options.Add("Please Select");
                foreach (var opt in options) Part4Options.Add(opt);
                if (Part4Options.Count == 2) SelectedPart4 = Part4Options[1];
            }
            else
            {
                LoadCoreItemOptionsAsync();
            }
            UpdateVisibilities();
        }

        private async void LoadCoreItemOptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading CoreItem options for P1-P4: {SelectedPart1}, {SelectedPart2}, {SelectedPart3}, {SelectedPart4}.");
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            // Part1 must be selected to get CoreItems
            if (string.IsNullOrEmpty(SelectedPart1) || SelectedPart1 == "Please Select")
            {
                UpdateVisibilities(); // This will hide CoreItem selector
                return;
            }

            // Adjust selected parts if they are "Please Select" to pass null to repository
            string p1 = (SelectedPart1 == "Please Select") ? null : SelectedPart1;
            string p2 = (SelectedPart2 == "Please Select" || string.IsNullOrEmpty(SelectedPart2)) ? null : SelectedPart2;
            string p3 = (SelectedPart3 == "Please Select" || string.IsNullOrEmpty(SelectedPart3)) ? null : SelectedPart3;
            string p4 = (SelectedPart4 == "Please Select" || string.IsNullOrEmpty(SelectedPart4)) ? null : SelectedPart4;

            // Check if the path is valid up to the point where parts are selected
            // e.g., if P2 is null, but P3 is selected, that's an invalid path typically
            // However, our repository handles NULLs in between for flexibility.
            // What matters is that P1 must be present.

            var options = await _dataRepository.GetDistinctCoreItemDisplayNamesAsync(_owningTableName, p1, p2, p3, p4);
            if (options.Any())
            {
                CoreItemOptions.Add("Please Select");
                foreach (var opt in options) CoreItemOptions.Add(opt);
                if (CoreItemOptions.Count == 2) SelectedCoreItem = CoreItemOptions[1]; // Auto-select
            }
            else
            {
                _logger?.LogWarning($"[ARLEVM {_owningTableName}] No CoreItems found for the selected path.");
            }
            UpdateVisibilities();
        }

        private async void UpdateActualTargetColumnNameAsync()
        {
            if (string.IsNullOrEmpty(SelectedPart1) || SelectedPart1 == "Please Select" ||
                string.IsNullOrEmpty(SelectedCoreItem) || SelectedCoreItem == "Please Select")
            {
                ActualTargetColumnName = null;
                IsValueEntryVisible = false;
                _logger?.LogInfo($"[ARLEVM {_owningTableName}] ActualTargetColumnName reset due to incomplete selection.");
                return;
            }

            string p1 = (SelectedPart1 == "Please Select") ? null : SelectedPart1; // Should not be null here by earlier checks
            string p2 = (SelectedPart2 == "Please Select" || string.IsNullOrEmpty(SelectedPart2)) ? null : SelectedPart2;
            string p3 = (SelectedPart3 == "Please Select" || string.IsNullOrEmpty(SelectedPart3)) ? null : SelectedPart3;
            string p4 = (SelectedPart4 == "Please Select" || string.IsNullOrEmpty(SelectedPart4)) ? null : SelectedPart4;
            string core = (SelectedCoreItem == "Please Select") ? null : SelectedCoreItem; // Should not be null

            ActualTargetColumnName = await _dataRepository.GetActualColumnNameAsync(_owningTableName, p1, p2, p3, p4, core);
            IsValueEntryVisible = !string.IsNullOrEmpty(ActualTargetColumnName);

            _logger?.LogInfo($"[ARLEVM {_owningTableName}] ActualTargetColumnName updated to: {ActualTargetColumnName ?? "NULL"}. Value entry visible: {IsValueEntryVisible}");
        }

        private void UpdateVisibilities()
        {
            // Part 2 is visible if Part 1 is selected and Part 2 has options (or could have options)
            IsPart2Visible = Part1Options.Any() && Part2Options.Any(opt => opt != "Please Select");

            // Part 3 is visible if Part 2 is selected (meaningfully) and Part 3 has options
            IsPart3Visible = IsPart2Visible && Part2Options.Any() && Part3Options.Any(opt => opt != "Please Select");

            // Part 4 is visible if Part 3 is selected (meaningfully) and Part 4 has options
            IsPart4Visible = IsPart3Visible && Part3Options.Any() && Part4Options.Any(opt => opt != "Please Select");

            // CoreItem ComboBox is visible if Part1 is selected, AND
            // ( (Part4 is visible AND selected meaningfully AND CoreItems exist for P1-P4) OR
            //   (Part3 is visible AND NOT Part4Visible AND selected meaningfully AND CoreItems exist for P1-P3) OR
            //   (Part2 is visible AND NOT Part3Visible AND selected meaningfully AND CoreItems exist for P1-P2) OR
            //   (NOT Part2Visible AND CoreItems exist for P1 only) )
            // Simplified: CoreItem is visible if its options are populated.
            IsCoreItemVisible = CoreItemOptions.Any(opt => opt != "Please Select");

            // Value entry is visible if an actual target column name has been resolved
            IsValueEntryVisible = !string.IsNullOrEmpty(ActualTargetColumnName);

            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Visibilities updated: P2Vis={IsPart2Visible}, P3Vis={IsPart3Visible}, P4Vis={IsPart4Visible}, CoreVis={IsCoreItemVisible}, ValVis={IsValueEntryVisible}");
        }


        // Helper for SetProperty to reduce boilerplate
        protected bool SetProperty<T>(ref T storage, T value, [System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}