using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class AddRowLongEntryViewModel : ViewModelBase
    {
        private readonly string _owningTableName;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        private string _selectedPart1;

        public string SelectedPart1
        {
            get => _selectedPart1;
            set
            {
                if (SetProperty(ref _selectedPart1, value))
                {
                    SelectedPart2 = null;
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
                    if (!string.IsNullOrEmpty(_selectedPart1) && _selectedPart2 != null)
                    {
                        LoadPart3OptionsAsync();
                    }
                    else if (string.IsNullOrEmpty(_selectedPart2) && !string.IsNullOrEmpty(_selectedPart1))
                    {
                        LoadCoreItemOptionsAsync();
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
                    if (!string.IsNullOrEmpty(_selectedPart1) && _selectedPart3 != null)
                    {
                        LoadPart4OptionsAsync();
                    }
                    else if (string.IsNullOrEmpty(_selectedPart3) && !string.IsNullOrEmpty(_selectedPart1))
                    {
                        LoadCoreItemOptionsAsync();
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
                    DisplayCoreItem = value;
                    UpdateActualTargetColumnNameAsync();
                }
            }
        }

        public ObservableCollection<string> Part1Options { get; private set; }
        public ObservableCollection<string> Part2Options { get; private set; }
        public ObservableCollection<string> Part3Options { get; private set; }
        public ObservableCollection<string> Part4Options { get; private set; }
        public ObservableCollection<string> CoreItemOptions { get; private set; }

        private bool _isPart2Visible;
        public bool IsPart2Visible { get => _isPart2Visible; private set => SetProperty(ref _isPart2Visible, value); }

        private bool _isPart3Visible;
        public bool IsPart3Visible { get => _isPart3Visible; private set => SetProperty(ref _isPart3Visible, value); }

        private bool _isPart4Visible;
        public bool IsPart4Visible { get => _isPart4Visible; private set => SetProperty(ref _isPart4Visible, value); }

        private bool _isCoreItemVisible;
        public bool IsCoreItemVisible { get => _isCoreItemVisible; private set => SetProperty(ref _isCoreItemVisible, value); }

        private bool _isValueEntryVisible;
        public bool IsValueEntryVisible { get => _isValueEntryVisible; private set => SetProperty(ref _isValueEntryVisible, value); }

        private string _displayCoreItem;
        public string DisplayCoreItem { get => _displayCoreItem; private set => SetProperty(ref _displayCoreItem, value); }

        private object _enteredValue;
        public object EnteredValue { get => _enteredValue; set => SetProperty(ref _enteredValue, value); }

        public string ActualTargetColumnName { get; private set; }

        public AddRowLongEntryViewModel(string owningTableName, IDataRepository dataRepository, ILogger logger)
        {
            _owningTableName = owningTableName;
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger;

            Part1Options = new ObservableCollection<string>();
            Part2Options = new ObservableCollection<string>();
            Part3Options = new ObservableCollection<string>();
            Part4Options = new ObservableCollection<string>();
            CoreItemOptions = new ObservableCollection<string>();

            LoadPart1OptionsAsync();
            UpdateVisibilities();
        }

        private async void LoadPart1OptionsAsync()
        {
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part1 options.");
            Part1Options.Clear();
            var options = await _dataRepository.GetDistinctPart1ValuesAsync(_owningTableName);
            options.Insert(0, "Please Select");
            foreach (var opt in options) Part1Options.Add(opt);

            if (Part1Options.Count == 2)
            {
                SelectedPart1 = Part1Options[1];
            }
            else if (Part1Options.Count <= 1)
            {
                _logger?.LogWarning($"[ARLEVM {_owningTableName}] No Part1 options found.");
            }
        }

        private async void LoadPart2OptionsAsync()
        {
            string capturedPart1 = SelectedPart1;
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part2 options for P1: {capturedPart1}.");
            Part2Options.Clear();
            Part3Options.Clear();
            Part4Options.Clear();
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(capturedPart1) || capturedPart1 == "Please Select")
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart2ValuesAsync(_owningTableName, capturedPart1);

            // FIX: Race condition check
            if (SelectedPart1 != capturedPart1) return;

            if (options.Any())
            {
                Part2Options.Add("Please Select");
                foreach (var opt in options) Part2Options.Add(opt);
                if (Part2Options.Count == 2) SelectedPart2 = Part2Options[1];
            }
            else
            {
                LoadCoreItemOptionsAsync();
            }
            UpdateVisibilities();
        }

        private async void LoadPart3OptionsAsync()
        {
            string capturedPart1 = SelectedPart1;
            string capturedPart2 = SelectedPart2;
            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part3 options.");
            Part3Options.Clear();
            Part4Options.Clear();
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(capturedPart1) || capturedPart1 == "Please Select" ||
                string.IsNullOrEmpty(capturedPart2) || capturedPart2 == "Please Select")
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart3ValuesAsync(_owningTableName, capturedPart1, capturedPart2);

            // FIX: Race condition check
            if (SelectedPart1 != capturedPart1 || SelectedPart2 != capturedPart2) return;

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
            string capturedPart1 = SelectedPart1;
            string capturedPart2 = SelectedPart2;
            string capturedPart3 = SelectedPart3;

            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading Part4 options.");
            Part4Options.Clear();
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(capturedPart1) || capturedPart1 == "Please Select" ||
                string.IsNullOrEmpty(capturedPart2) || capturedPart2 == "Please Select" ||
                string.IsNullOrEmpty(capturedPart3) || capturedPart3 == "Please Select")
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctPart4ValuesAsync(_owningTableName, capturedPart1, capturedPart2, capturedPart3);

            // FIX: Race condition check
            if (SelectedPart1 != capturedPart1 || SelectedPart2 != capturedPart2 || SelectedPart3 != capturedPart3) return;

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
            string p1 = SelectedPart1 == "Please Select" ? null : SelectedPart1;
            string p2 = (SelectedPart2 == "Please Select" || string.IsNullOrEmpty(SelectedPart2)) ? null : SelectedPart2;
            string p3 = (SelectedPart3 == "Please Select" || string.IsNullOrEmpty(SelectedPart3)) ? null : SelectedPart3;
            string p4 = (SelectedPart4 == "Please Select" || string.IsNullOrEmpty(SelectedPart4)) ? null : SelectedPart4;

            _logger?.LogInfo($"[ARLEVM {_owningTableName}] Loading CoreItem options.");
            CoreItemOptions.Clear();
            DisplayCoreItem = string.Empty;
            ActualTargetColumnName = null;

            if (string.IsNullOrEmpty(p1))
            {
                UpdateVisibilities();
                return;
            }

            var options = await _dataRepository.GetDistinctCoreItemDisplayNamesAsync(_owningTableName, p1, p2, p3, p4);

            // FIX: Race condition check
            string curP1 = SelectedPart1 == "Please Select" ? null : SelectedPart1;
            string curP2 = (SelectedPart2 == "Please Select" || string.IsNullOrEmpty(SelectedPart2)) ? null : SelectedPart2;
            string curP3 = (SelectedPart3 == "Please Select" || string.IsNullOrEmpty(SelectedPart3)) ? null : SelectedPart3;
            string curP4 = (SelectedPart4 == "Please Select" || string.IsNullOrEmpty(SelectedPart4)) ? null : SelectedPart4;
            if (curP1 != p1 || curP2 != p2 || curP3 != p3 || curP4 != p4) return;

            if (options.Any())
            {
                CoreItemOptions.Add("Please Select");
                foreach (var opt in options) CoreItemOptions.Add(opt);
                if (CoreItemOptions.Count == 2) SelectedCoreItem = CoreItemOptions[1];
            }
            else
            {
                _logger?.LogWarning($"[ARLEVM {_owningTableName}] No CoreItems found.");
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
                _logger?.LogInfo($"[ARLEVM {_owningTableName}] ActualTargetColumnName reset.");
                return;
            }

            string p1 = (SelectedPart1 == "Please Select") ? null : SelectedPart1;
            string p2 = (SelectedPart2 == "Please Select" || string.IsNullOrEmpty(SelectedPart2)) ? null : SelectedPart2;
            string p3 = (SelectedPart3 == "Please Select" || string.IsNullOrEmpty(SelectedPart3)) ? null : SelectedPart3;
            string p4 = (SelectedPart4 == "Please Select" || string.IsNullOrEmpty(SelectedPart4)) ? null : SelectedPart4;
            string core = (SelectedCoreItem == "Please Select") ? null : SelectedCoreItem;

            ActualTargetColumnName = await _dataRepository.GetActualColumnNameAsync(_owningTableName, p1, p2, p3, p4, core);
            IsValueEntryVisible = !string.IsNullOrEmpty(ActualTargetColumnName);

            _logger?.LogInfo($"[ARLEVM {_owningTableName}] ActualTargetColumnName updated to: {ActualTargetColumnName ?? "NULL"}");
        }

        private void UpdateVisibilities()
        {
            IsPart2Visible = Part1Options.Any() && Part2Options.Any(opt => opt != "Please Select");
            IsPart3Visible = IsPart2Visible && Part2Options.Any() && Part3Options.Any(opt => opt != "Please Select");
            IsPart4Visible = IsPart3Visible && Part3Options.Any() && Part4Options.Any(opt => opt != "Please Select");
            IsCoreItemVisible = CoreItemOptions.Any(opt => opt != "Please Select");
            IsValueEntryVisible = !string.IsNullOrEmpty(ActualTargetColumnName);
        }
    }
}