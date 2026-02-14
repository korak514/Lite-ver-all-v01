using FontAwesome.Sharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Views; // Ensure you have created ErrorDrillDownWindow in Views
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.ViewModels
{
    public enum AppMode
    { Normal, ReportOnly, SettingsOnly }

    public class MainViewModel : ViewModelBase
    {
        // --- Core Fields ---
        private UserAccountModel _currentUserAccount;

        private ViewModelBase _currentChildView;
        private string _caption;
        private IconChar _icon;
        private bool _isNavigationVisible = true;
        private bool _isOfflineMode = false;

        // --- Services ---
        private readonly IUserRepository _userRepository;

        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        // --- Cached ViewModels (State Preservation) ---
        private HomeViewModel _homeViewModel;

        private CustomerViewModel _customerViewModel;
        private DatarepViewModel _datarepViewModel;
        private InventoryViewModel _inventoryViewModel;
        private SettingsViewModel _settingsViewModel;
        private HelpViewModel _helpViewModel;
        private ErrorManagementViewModel _errorViewModel;

        // --- Properties ---
        public UserAccountModel CurrentUserAccount
        { get => _currentUserAccount; set { _currentUserAccount = value; OnPropertyChanged(); } }

        public ViewModelBase CurrentChildView
        { get => _currentChildView; set { if (_currentChildView != null && _currentChildView != value) DeactivateCurrentView(); _currentChildView = value; OnPropertyChanged(); ActivateCurrentView(); } }

        public string Caption
        { get => _caption; set { _caption = value; OnPropertyChanged(); } }

        public IconChar Icon
        { get => _icon; set { _icon = value; OnPropertyChanged(); } }

        public bool IsNavigationVisible
        { get => _isNavigationVisible; set { _isNavigationVisible = value; OnPropertyChanged(); } }

        public bool IsOfflineMode
        { get => _isOfflineMode; set { _isOfflineMode = value; OnPropertyChanged(); } }

        // --- Commands ---
        public ICommand ShowHomeViewCommand { get; }

        public ICommand ShowCustomerViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }
        public ICommand ShowInventoryViewCommand { get; }
        public ICommand ShowSettingsViewCommand { get; }
        public ICommand ShowHelpViewCommand { get; }
        public ICommand ReturnToLoginCommand { get; }
        public ICommand ShowErrorViewCommand { get; }

        // --- Constructor ---
        public MainViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Fallback_Log");
            _dialogService = new DialogService();
            _userRepository = new UserRepository();
            _dataRepository = new DataRepository(_logger);

            // Placeholder to prevent null binding errors
            CurrentUserAccount = new UserAccountModel { DisplayName = "Loading..." };

            // Initialize Commands
            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowHelpViewCommand = new ViewModelCommand(ExecuteShowHelpViewCommand);
            ShowErrorViewCommand = new ViewModelCommand(ExecuteShowErrorViewCommand);
            ReturnToLoginCommand = new ViewModelCommand(ExecuteReturnToLogin);

            Task.Run(() => LoadCurrentUserDataAsync());

            _logger.LogInfo("Main Dashboard initialized.");
        }

        public void Initialize(AppMode mode)
        {
            if (mode == AppMode.SettingsOnly)
            {
                IsNavigationVisible = false;
                IsOfflineMode = true;
                ExecuteShowSettingsViewCommand(null);
                _logger.LogInfo("Started in 'Settings Only' Mode.");
                CurrentUserAccount = new UserAccountModel { DisplayName = "Offline Config" };
            }
            else if (mode == AppMode.ReportOnly)
            {
                IsNavigationVisible = false;
                IsOfflineMode = false;
                ExecuteShowReportsViewCommand(null);
                _logger.LogInfo("Started in 'Only Report' Mode.");
            }
            else
            {
                IsNavigationVisible = true;
                IsOfflineMode = false;
                ExecuteShowHomeViewCommand(null);
                _logger.LogInfo("Started in Normal Mode.");
            }
        }

        // --- Navigation Logic ---

        private void DeactivateCurrentView()
        {
            if (_currentChildView is HomeViewModel homeViewModel)
            {
                homeViewModel.Deactivate();
            }
            else if (_currentChildView is ErrorManagementViewModel errorViewModel)
            {
                // We notify it's inactive, but we WON'T stop the data timer (per your request)
                errorViewModel.IsActiveView = false;
            }
        }

        private void ActivateCurrentView()
        {
            if (_currentChildView is HomeViewModel homeViewModel)
            {
                homeViewModel.Activate();
            }
            else if (_currentChildView is ErrorManagementViewModel errorViewModel)
            {
                // This wakes up the charts "like the dashboard page"
                errorViewModel.Activate();
            }
        }

        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_homeViewModel == null)
            {
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService, _logger);
                // Hook up legacy drill down if needed, or remove if using new system
                _homeViewModel.DrillDownRequested += OnDashboardDrillDown;
            }
            CurrentChildView = _homeViewModel;
            Caption = Resources.Nav_Dashboard;
            Icon = IconChar.Home;
        }

        // Legacy Drill Down (Home -> Reports)
        private void OnDashboardDrillDown(string tableName, DateTime start, DateTime end)
        {
            ExecuteShowReportsViewCommand(null);
            if (_datarepViewModel != null)
            {
                _datarepViewModel.LoadTableWithFilter(tableName, start, end);
            }
        }

        private void ExecuteShowCustomerViewCommand(object obj)
        {
            if (_customerViewModel == null) _customerViewModel = new CustomerViewModel(_dataRepository);
            CurrentChildView = _customerViewModel;
            Caption = Resources.Nav_Customers;
            Icon = IconChar.UserGroup;
        }

        private void ExecuteShowInventoryViewCommand(object obj)
        {
            if (_inventoryViewModel == null) _inventoryViewModel = new InventoryViewModel(_dataRepository, _dialogService);
            CurrentChildView = _inventoryViewModel;
            Caption = Resources.Nav_Logs;
            Icon = IconChar.ListAlt;
        }

        private void ExecuteShowReportsViewCommand(object obj)
        {
            if (_datarepViewModel == null) _datarepViewModel = new DatarepViewModel(_logger, _dialogService, _dataRepository);
            CurrentChildView = _datarepViewModel;
            Caption = Resources.Nav_Reports;
            Icon = IconChar.BarChart;
        }

        private void ExecuteShowSettingsViewCommand(object obj)
        {
            if (_settingsViewModel == null) _settingsViewModel = new SettingsViewModel();
            CurrentChildView = _settingsViewModel;
            Caption = Resources.Nav_Settings;
            Icon = IconChar.Cogs;
        }

        private void ExecuteShowHelpViewCommand(object obj)
        {
            if (_helpViewModel == null) _helpViewModel = new HelpViewModel();
            CurrentChildView = _helpViewModel;
            Caption = Resources.Nav_Help;
            Icon = IconChar.QuestionCircle;
        }

        // --- NEW: Error Analytics Navigation & Drill Down ---

        private void ExecuteShowErrorViewCommand(object obj)
        {
            if (_errorViewModel == null)
            {
                _errorViewModel = new ErrorManagementViewModel(_dataRepository);
                _errorViewModel.DrillDownRequested += OnErrorDrillDown;
            }

            CurrentChildView = _errorViewModel; // This triggers Deactivate old -> Activate new
            Caption = "Error Analytics";
            Icon = IconChar.PieChart;
        }

        // This runs when you click a chart slice in Error Analytics
        private async void OnErrorDrillDown(string tableName, DateTime start, DateTime end, string filterText)
        {
            try
            {
                // 1. Fetch Raw Data (All errors for this table and date range)
                var rawData = await _dataRepository.GetErrorDataAsync(start, end, tableName);

                // 2. Convert ALL to UI models (Do not filter here!)
                var uiList = new ObservableCollection<ErrorLogItem>();

                foreach (var item in rawData)
                {
                    var logItem = new ErrorLogItem
                    {
                        Date = item.Date,
                        Shift = item.Shift,
                        StartTime = FormatTime(item.StartTime),
                        DurationMinutes = item.DurationMinutes,
                        MachineCode = "MA-" + item.MachineCode, // Ensure prefix consistency
                        ErrorMessage = item.ErrorDescription,
                        EndTime = CalculateEndTime(item.StartTime, item.DurationMinutes)
                    };
                    uiList.Add(logItem);
                }

                // 3. Open Window (Pass the FULL list and the FILTER TEXT)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // We pass 'filterText' so the ViewModel knows what chip to create initially
                    var drillDownVM = new ErrorDrillDownViewModel(uiList, tableName, filterText);
                    var drillDownWindow = new ErrorDrillDownWindow { DataContext = drillDownVM };

                    if (Application.Current.MainWindow != null)
                        drillDownWindow.Owner = Application.Current.MainWindow;

                    drillDownWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error DrillDown Failed: {ex.Message}", ex);
                MessageBox.Show($"Could not open details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Helper to format "0435" to "04:35"
        private string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length < 4) return raw;
            if (raw.Contains(":")) return raw; // Already formatted
            return raw.Insert(2, ":");
        }

        private string CalculateEndTime(string start, int duration)
        {
            try
            {
                string formattedStart = FormatTime(start);
                if (TimeSpan.TryParse(formattedStart, out TimeSpan ts))
                {
                    return ts.Add(TimeSpan.FromMinutes(duration)).ToString(@"hh\:mm");
                }
            }
            catch { }
            return "?";
        }

        // --- Logout & Cleanup ---

        private void ExecuteReturnToLogin(object obj)
        {
            UserSessionService.Logout();
            _logger.LogInfo("User Logged Out.");

            // Clear cached views to prevent data leaking
            _homeViewModel = null;
            _customerViewModel = null;
            _datarepViewModel = null;
            _inventoryViewModel = null;
            _settingsViewModel = null;
            _helpViewModel = null;

            if (_errorViewModel != null)
            {
                _errorViewModel.DrillDownRequested -= OnErrorDrillDown;
                _errorViewModel = null;
            }

            CurrentChildView = null;

            // Handle Window Switching
            var loginView = new LoginView();

            loginView.IsVisibleChanged += (s, ev) =>
            {
                if (loginView.IsVisible == false && loginView.IsLoaded && loginView.WindowState != WindowState.Minimized)
                {
                    var loginVM = loginView.DataContext as LoginViewModel;
                    if (loginVM != null && !loginVM.IsViewVisible)
                    {
                        var newMain = new MainView();
                        if (newMain.DataContext is MainViewModel mainVM)
                        {
                            if (loginVM.IsSettingsModeOnly) mainVM.Initialize(AppMode.SettingsOnly);
                            else mainVM.Initialize(loginVM.IsReportModeOnly ? AppMode.ReportOnly : AppMode.Normal);
                        }
                        newMain.Show();
                        loginView.Close();
                    }
                }
            };

            loginView.Show();

            foreach (Window win in Application.Current.Windows)
            {
                if (win.DataContext == this)
                {
                    win.Close();
                    break;
                }
            }
        }

        private async Task LoadCurrentUserDataAsync()
        {
            string username = UserSessionService.CurrentUsername;

            if (Application.Current == null) return;

            if (!string.IsNullOrEmpty(username))
            {
                var user = await Task.Run(() => _userRepository.GetByUsername(username));

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (user != null)
                    {
                        CurrentUserAccount = new UserAccountModel
                        {
                            Username = user.Username,
                            DisplayName = $"{user.Name} {user.LastName}",
                            Role = UserSessionService.CurrentRole,
                            ProfilePicture = null
                        };
                    }
                    else
                    {
                        CurrentUserAccount = new UserAccountModel { DisplayName = "Unknown User" };
                    }
                });
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    CurrentUserAccount = new UserAccountModel { DisplayName = "Not logged in" });
            }
        }
    }
}