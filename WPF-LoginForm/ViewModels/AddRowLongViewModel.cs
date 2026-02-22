using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class AddRowLongViewModel : ViewModelBase
    {
        private readonly string _owningTableName;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;
        private readonly IDialogService _dialogService;
        private string _resolvedDateColumnName;

        // --- Date Control Properties ---
        private bool _isDateToday = true;

        public bool IsDateToday
        {
            get => _isDateToday;
            set
            {
                if (SetProperty(ref _isDateToday, value, nameof(IsDateToday)))
                {
                    OnPropertyChanged(nameof(IsDatePickerEnabled));
                    UpdateEntryDate();
                }
            }
        }

        public bool IsDatePickerEnabled => !_isDateToday;

        private DateTime _lastKnownEntryDate;

        private DateTime _entryDate;

        public DateTime EntryDate
        {
            get => _entryDate;
            set => SetProperty(ref _entryDate, value, nameof(EntryDate));
        }

        public ObservableCollection<AddRowLongEntryViewModel> EntryLines { get; private set; }

        public ICommand AddEntryLineCommand { get; }
        public ICommand RemoveEntryLineCommand { get; }

        private string _windowTitle;

        public string WindowTitle
        {
            get => _windowTitle;
            private set => SetProperty(ref _windowTitle, value, nameof(WindowTitle));
        }

        private DataTable _targetTableSchema;

        public AddRowLongViewModel(string owningTableName, IDataRepository dataRepository, ILogger logger, IDialogService dialogService)
        {
            _owningTableName = owningTableName ?? throw new ArgumentNullException(nameof(owningTableName));
            _dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
            _logger = logger;
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            WindowTitle = $"Add New Detailed Row to '{_owningTableName}'";

            EntryLines = new ObservableCollection<AddRowLongEntryViewModel>();

            AddEntryLineCommand = new ViewModelCommand(ExecuteAddEntryLine);
            RemoveEntryLineCommand = new ViewModelCommand(ExecuteRemoveEntryLine, CanExecuteRemoveEntryLine);

            InitializeViewModelAsync();
        }

        private async void InitializeViewModelAsync()
        {
            await LoadTargetTableSchemaAsync();
            await FindLastEntryDateAsync();
            UpdateEntryDate();
            AddInitialEntryLines(1);
        }

        private void UpdateEntryDate()
        {
            if (IsDateToday)
            {
                EntryDate = DateTime.Today;
            }
            else
            {
                EntryDate = (_lastKnownEntryDate == DateTime.MinValue)
                    ? DateTime.Today.AddDays(1)
                    : _lastKnownEntryDate.AddDays(1);
            }
            _logger?.LogInfo($"[ARLVM] EntryDate updated to: {EntryDate.ToShortDateString()} (IsDateToday={IsDateToday})");
        }

        private async Task FindLastEntryDateAsync()
        {
            _lastKnownEntryDate = DateTime.MinValue;
            if (_targetTableSchema == null || string.IsNullOrEmpty(_resolvedDateColumnName))
            {
                _logger?.LogWarning($"[ARLVM] Cannot find last entry date: valid date column not found in schema for {_owningTableName}.");
                return;
            }

            try
            {
                var result = await _dataRepository.GetTableDataAsync(_owningTableName, 1);
                DataTable fullTable = result.Data;

                var lastDate = fullTable.AsEnumerable()
                                        .Select(row => row[_resolvedDateColumnName] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row[_resolvedDateColumnName]))
                                        .Where(d => d.HasValue)
                                        .OrderByDescending(d => d.Value)
                                        .FirstOrDefault();
                if (lastDate.HasValue)
                {
                    _lastKnownEntryDate = lastDate.Value;
                    _logger?.LogInfo($"[ARLVM] Found last entry date: {_lastKnownEntryDate.ToShortDateString()}");
                }
                else
                {
                    _logger?.LogInfo($"[ARLVM] No previous entries with dates found in {_owningTableName}.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ARLVM] Error finding last entry date for {_owningTableName}.", ex);
            }
        }

        private async Task LoadTargetTableSchemaAsync()
        {
            try
            {
                var result = await _dataRepository.GetTableDataAsync(_owningTableName, 1);
                _targetTableSchema = result.Data;

                // FIX Bug 5: Dynamically detect the date column instead of hardcoding "EntryDate"
                _resolvedDateColumnName = _targetTableSchema.Columns.Cast<DataColumn>()
                    .FirstOrDefault(c => c.ColumnName.Equals("EntryDate", StringComparison.OrdinalIgnoreCase) ||
                                         c.ColumnName.Equals("Date", StringComparison.OrdinalIgnoreCase) ||
                                         c.ColumnName.Equals("Tarih", StringComparison.OrdinalIgnoreCase))?.ColumnName;

                _logger?.LogInfo($"[ARLVM {_owningTableName}] Target table schema loaded. Date column resolved to: {_resolvedDateColumnName}");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"[ARLVM {_owningTableName}] Failed to load target table schema: {ex.Message}", ex);
                _dialogService.ShowConfirmationDialog("Schema Error", $"Could not load schema for table '{_owningTableName}'. Error: {ex.Message}");
                _targetTableSchema = new DataTable();
            }
        }

        private void AddInitialEntryLines(int count)
        {
            for (int i = 0; i < count; i++)
            {
                ExecuteAddEntryLine(null);
            }
        }

        private void ExecuteAddEntryLine(object parameter)
        {
            _logger?.LogInfo($"[ARLVM {_owningTableName}] Adding new entry line.");
            var newEntry = new AddRowLongEntryViewModel(_owningTableName, _dataRepository, _logger);
            EntryLines.Add(newEntry);
        }

        private bool CanExecuteRemoveEntryLine(object parameter)
        {
            return EntryLines.Count > 0 && parameter is AddRowLongEntryViewModel;
        }

        private void ExecuteRemoveEntryLine(object parameter)
        {
            if (parameter is AddRowLongEntryViewModel entryToRemove)
            {
                _logger?.LogInfo($"[ARLVM {_owningTableName}] Removing entry line.");
                EntryLines.Remove(entryToRemove);
            }
        }

        public NewRowData GetEnteredData(out List<string> validationErrors)
        {
            validationErrors = new List<string>();
            var newRowData = new NewRowData();

            if (_targetTableSchema != null && !string.IsNullOrEmpty(_resolvedDateColumnName))
            {
                newRowData.Values[_resolvedDateColumnName] = this.EntryDate;
            }
            else
            {
                _logger?.LogWarning($"[ARLVM GetEnteredData] No valid date column found in schema for {_owningTableName}. Date will not be included in NewRowData.");
            }

            if (_targetTableSchema == null)
            {
                validationErrors.Add("Target table schema not loaded. Cannot validate or convert data types.");
                _logger?.LogError("[ARLVM] GetEnteredData called but _targetTableSchema is null.");
                return null;
            }

            int lineNum = 0;
            foreach (var entryLine in EntryLines)
            {
                lineNum++;
                if (string.IsNullOrEmpty(entryLine.ActualTargetColumnName))
                {
                    if (entryLine.EnteredValue != null && !string.IsNullOrWhiteSpace(entryLine.EnteredValue.ToString()))
                    {
                        validationErrors.Add($"Line {lineNum}: Value '{entryLine.EnteredValue}' entered but no target column selected/resolved.");
                    }
                    continue;
                }

                if (newRowData.Values.ContainsKey(entryLine.ActualTargetColumnName))
                {
                    validationErrors.Add($"Line {lineNum}: Duplicate entry for column '{entryLine.ActualTargetColumnName}'.");
                    continue;
                }

                DataColumn columnSchema = _targetTableSchema.Columns[entryLine.ActualTargetColumnName];
                if (columnSchema == null)
                {
                    validationErrors.Add($"Line {lineNum}: Target column '{entryLine.ActualTargetColumnName}' not found in table schema.");
                    continue;
                }

                if (entryLine.EnteredValue == null || string.IsNullOrWhiteSpace(entryLine.EnteredValue.ToString()))
                {
                    if (!columnSchema.AllowDBNull)
                    {
                        validationErrors.Add($"Line {lineNum} ({entryLine.ActualTargetColumnName}): Value is required but not provided.");
                    }
                    else
                    {
                        newRowData.Values[entryLine.ActualTargetColumnName] = DBNull.Value;
                    }
                }
                else
                {
                    try
                    {
                        object convertedValue = Convert.ChangeType(entryLine.EnteredValue, columnSchema.DataType, CultureInfo.CurrentCulture);
                        newRowData.Values[entryLine.ActualTargetColumnName] = convertedValue;
                    }
                    catch (Exception ex)
                    {
                        validationErrors.Add($"Line {lineNum} ({entryLine.ActualTargetColumnName}): Cannot convert value '{entryLine.EnteredValue}' to type '{columnSchema.DataType.Name}'. Error: {ex.Message}");
                    }
                }
            }

            if (validationErrors.Any())
            {
                return null;
            }

            return newRowData;
        }
    }
}