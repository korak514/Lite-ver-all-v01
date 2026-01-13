using System;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_LoginForm.Repositories;

namespace WPF_LoginForm.ViewModels
{
    public class CustomerViewModel : ViewModelBase
    {
        private readonly IDataRepository _dataRepository;
        private DataView _customersData;
        private string _searchText;
        private bool _isBusy;
        private string _statusMessage;

        // The exact table name in the database we are looking for
        private const string TargetTableName = "Customers";

        public DataView CustomersData
        {
            get => _customersData;
            set => SetProperty(ref _customersData, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
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

        public ICommand SearchCommand { get; }
        public ICommand ReloadCommand { get; }

        public CustomerViewModel(IDataRepository dataRepository)
        {
            _dataRepository = dataRepository;
            SearchCommand = new ViewModelCommand(p => ApplyFilter());
            ReloadCommand = new ViewModelCommand(p => LoadDataAsync());

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading customers...";
            try
            {
                // Check if table exists first to avoid crashing
                bool exists = await _dataRepository.TableExistsAsync(TargetTableName);
                if (!exists)
                {
                    StatusMessage = $"Table '{TargetTableName}' not found. Please create it in Reports.";
                    IsBusy = false;
                    return;
                }

                // FIX: Handle tuple return (Data, IsSortable)
                // We just need the data here, so we extract .Data
                var result = await _dataRepository.GetTableDataAsync(TargetTableName);
                DataTable dt = result.Data;

                CustomersData = dt.DefaultView;
                StatusMessage = $"{dt.Rows.Count} customers loaded.";
                ApplyFilter();
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

        private void ApplyFilter()
        {
            if (CustomersData == null) return;

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                CustomersData.RowFilter = string.Empty;
            }
            else
            {
                try
                {
                    // --- FIX: Escape ---
                    string safeSearch = SearchText
                        .Replace("'", "''")
                        .Replace("[", "[[]")
                        .Replace("%", "[%]")
                        .Replace("*", "[*]");

                    if (CustomersData.Table.Columns.Contains("Name"))
                        CustomersData.RowFilter = $"Name LIKE '%{safeSearch}%'";
                    // ... etc
                }
                catch
                {
                    // Ignore invalid filter input while typing
                }
            }
        }
    }
}