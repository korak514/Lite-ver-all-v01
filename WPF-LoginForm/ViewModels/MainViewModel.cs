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
using WPF_LoginForm.Views;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.ViewModels
{
    public enum AppMode
    { Normal, ReportOnly, SettingsOnly, OfflineReadOnly }

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
        private IDataRepository _dataRepository;
        private readonly ILogger _logger;

        // --- Cached ViewModels ---
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

        public MainViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Fallback_Log");
            _dialogService = new DialogService();
            _userRepository = new UserRepository();
            _dataRepository = new DataRepository(_logger);

            CurrentUserAccount = new UserAccountModel { DisplayName = "Loading..." };

            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowHelpViewCommand = new ViewModelCommand(ExecuteShowHelpViewCommand);
            ShowErrorViewCommand = new ViewModelCommand(ExecuteShowErrorViewCommand);
            ReturnToLoginCommand = new ViewModelCommand(ExecuteReturnToLogin);

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
                Task.Run(() => LoadCurrentUserDataAsync());
            }
            else if (mode == AppMode.OfflineReadOnly)
            {
                IsNavigationVisible = true;
                IsOfflineMode = true;

                _dataRepository = new OfflineDataRepository(_logger);

                UserSessionService.SetSession("Offline Mode", "Admin");
                CurrentUserAccount = new UserAccountModel { DisplayName = "Offline Mode (Read-Only)", Role = "Admin" };

                ExecuteShowHomeViewCommand(null);
                _logger.LogInfo("Started in 'Offline Read-Only' Mode.");
            }
            else
            {
                IsNavigationVisible = true;
                IsOfflineMode = false;
                ExecuteShowHomeViewCommand(null);
                _logger.LogInfo("Started in Normal Mode.");
                Task.Run(() => LoadCurrentUserDataAsync());
            }
        }

        private void DeactivateCurrentView()
        {
            if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Deactivate();
            else if (_currentChildView is ErrorManagementViewModel errorViewModel) errorViewModel.IsActiveView = false;
        }

        private void ActivateCurrentView()
        {
            if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Activate();
            else if (_currentChildView is ErrorManagementViewModel errorViewModel) errorViewModel.Activate();
        }

        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_homeViewModel == null)
            {
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService, _logger);
                _homeViewModel.DrillDownRequested += OnDashboardDrillDown;
            }
            CurrentChildView = _homeViewModel;
            Caption = Resources.Nav_Dashboard;
            Icon = IconChar.Home;
        }

        private void OnDashboardDrillDown(string tableName, DateTime start, DateTime end)
        {
            ExecuteShowReportsViewCommand(null);
            if (_datarepViewModel != null) _datarepViewModel.LoadTableWithFilter(tableName, start, end);
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
            // FIX: Pass the DataRepository to Settings so it can create the Offline Backup
            if (_settingsViewModel == null) _settingsViewModel = new SettingsViewModel(_dataRepository);
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

        private void ExecuteShowErrorViewCommand(object obj)
        {
            if (_errorViewModel == null)
            {
                _errorViewModel = new ErrorManagementViewModel(_dataRepository);
                _errorViewModel.DrillDownRequested += OnErrorDrillDown;
            }
            CurrentChildView = _errorViewModel;
            Caption = "Error Analytics";
            Icon = IconChar.PieChart;
        }

        private async void OnErrorDrillDown(string tableName, DateTime start, DateTime end, string filterText)
        {
            try
            {
                var rawData = await _dataRepository.GetErrorDataAsync(start, end, tableName);
                var uiList = new ObservableCollection<ErrorLogItem>();
                foreach (var item in rawData)
                {
                    var logItem = new ErrorLogItem
                    {
                        Date = item.Date,
                        Shift = item.Shift,
                        StartTime = FormatTime(item.StartTime),
                        DurationMinutes = item.DurationMinutes,
                        MachineCode = "MA-" + item.MachineCode,
                        ErrorMessage = item.ErrorDescription,
                        EndTime = CalculateEndTime(item.StartTime, item.DurationMinutes)
                    };
                    uiList.Add(logItem);
                }

                bool useClock = _errorViewModel?.IsMinToClockFormat ?? false;
                bool exclude00 = _errorViewModel?.IsMachine00Excluded ?? false;

                List<string> excludedMachines = new List<string>();
                string effectiveFilter = filterText;

                if (filterText.StartsWith("MACHINE_OTHERS|"))
                {
                    var parts = filterText.Split('|');
                    effectiveFilter = "MACHINE_OTHERS";
                    if (parts.Length > 1)
                    {
                        var codes = parts[1].Split(',');
                        foreach (var c in codes) excludedMachines.Add(c.StartsWith("MA-") ? c : "MA-" + c);
                    }
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    var drillDownVM = new ErrorDrillDownViewModel(uiList, tableName, effectiveFilter, excludedMachines, useClock, exclude00);
                    var drillDownWindow = new ErrorDrillDownWindow { DataContext = drillDownVM };
                    if (Application.Current.MainWindow != null) drillDownWindow.Owner = Application.Current.MainWindow;
                    drillDownWindow.ShowDialog();
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error DrillDown Failed: {ex.Message}", ex);
                MessageBox.Show($"Could not open details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Length < 4) return raw;
            if (raw.Contains(":")) return raw;
            return raw.Insert(2, ":");
        }

        private string CalculateEndTime(string start, int duration)
        {
            try
            {
                string formattedStart = FormatTime(start);
                if (TimeSpan.TryParse(formattedStart, out TimeSpan ts)) return ts.Add(TimeSpan.FromMinutes(duration)).ToString(@"hh\:mm");
            }
            catch { }
            return "?";
        }

        private void ExecuteReturnToLogin(object obj)
        {
            UserSessionService.Logout();
            _logger.LogInfo("User Logged Out.");

            _homeViewModel = null; _customerViewModel = null; _datarepViewModel = null;
            _inventoryViewModel = null; _settingsViewModel = null; _helpViewModel = null;
            if (_errorViewModel != null) { _errorViewModel.DrillDownRequested -= OnErrorDrillDown; _errorViewModel = null; }
            CurrentChildView = null;

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
                            else if (loginVM.IsOfflineModeOnly) mainVM.Initialize(AppMode.OfflineReadOnly);
                            else mainVM.Initialize(loginVM.IsReportModeOnly ? AppMode.ReportOnly : AppMode.Normal);
                        }
                        newMain.Show();
                        loginView.Close();
                    }
                }
            };

            loginView.Show();
            foreach (Window win in Application.Current.Windows) { if (win.DataContext == this) { win.Close(); break; } }
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
                    if (user != null) CurrentUserAccount = new UserAccountModel { Username = user.Username, DisplayName = $"{user.Name} {user.LastName}", Role = UserSessionService.CurrentRole, ProfilePicture = null };
                    else CurrentUserAccount = new UserAccountModel { DisplayName = "Unknown User" };
                });
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() => CurrentUserAccount = new UserAccountModel { DisplayName = "Not logged in" });
            }
        }
    }
}