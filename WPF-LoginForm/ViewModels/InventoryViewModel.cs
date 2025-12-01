using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private DataTable _logsTable;
        private bool _isBusy;
        private string _statusMessage;

        public DataTable LogsTable
        {
            get => _logsTable;
            set => SetProperty(ref _logsTable, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand RefreshLogsCommand { get; }
        public ICommand ClearLogsCommand { get; }

        // Constructor now requires Dependencies
        public InventoryViewModel(IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;

            RefreshLogsCommand = new ViewModelCommand(ExecuteRefreshLogs);
            ClearLogsCommand = new ViewModelCommand(ExecuteClearLogs);

            // Load logs automatically on creation
            ExecuteRefreshLogs(null);
        }

        private async void ExecuteRefreshLogs(object obj)
        {
            IsBusy = true;
            StatusMessage = "Loading logs...";
            try
            {
                var data = await _dataRepository.GetSystemLogsAsync();
                LogsTable = data;
                StatusMessage = $"Loaded {data.Rows.Count} log entries.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading logs: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void ExecuteClearLogs(object obj)
        {
            bool confirm = _dialogService.ShowConfirmationDialog("Clear System Logs",
                "Are you sure you want to delete ALL system logs? This cannot be undone.");

            if (!confirm) return;

            IsBusy = true;
            try
            {
                bool success = await _dataRepository.ClearSystemLogsAsync();
                if (success)
                {
                    LogsTable?.Clear();
                    StatusMessage = "System logs cleared successfully.";
                }
                else
                {
                    StatusMessage = "Failed to clear logs.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}