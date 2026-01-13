using FontAwesome.Sharp;
using System.Threading;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using System;
using System.Windows;
using WPF_LoginForm.Views;
using System.Threading.Tasks;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.ViewModels
{
    public enum AppMode
    { Normal, ReportOnly, SettingsOnly }

    public class MainViewModel : ViewModelBase
    {
        private UserAccountModel _currentUserAccount;
        private ViewModelBase _currentChildView;
        private string _caption;
        private IconChar _icon;
        private bool _isNavigationVisible = true;
        private bool _isOfflineMode = false;

        private readonly IUserRepository _userRepository;
        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        private HomeViewModel _homeViewModel;
        private CustomerViewModel _customerViewModel;
        private DatarepViewModel _datarepViewModel;
        private InventoryViewModel _inventoryViewModel;
        private SettingsViewModel _settingsViewModel;
        private HelpViewModel _helpViewModel;

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

        public ICommand ShowHomeViewCommand { get; }
        public ICommand ShowCustomerViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }
        public ICommand ShowInventoryViewCommand { get; }
        public ICommand ShowSettingsViewCommand { get; }
        public ICommand ShowHelpViewCommand { get; }
        public ICommand ReturnToLoginCommand { get; }

        public MainViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Fallback_Log");
            _dialogService = new DialogService();
            _userRepository = new UserRepository();
            _dataRepository = new DataRepository(_logger);

            // Initialize with placeholder to prevent null binding errors
            CurrentUserAccount = new UserAccountModel { DisplayName = "Loading..." };

            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowHelpViewCommand = new ViewModelCommand(ExecuteShowHelpViewCommand);
            ReturnToLoginCommand = new ViewModelCommand(ExecuteReturnToLogin);

            // Load User Data Asynchronously
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
                // Create NEW object to trigger UI update
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

        private void ExecuteReturnToLogin(object obj)
        {
            UserSessionService.Logout();
            _logger.LogInfo("User Logged Out.");

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

        private void DeactivateCurrentView()
        { if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Deactivate(); }

        private void ActivateCurrentView()
        { if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Activate(); }

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

        private async Task LoadCurrentUserDataAsync()
        {
            string username = UserSessionService.CurrentUsername;

            if (!string.IsNullOrEmpty(username))
            {
                var user = _userRepository.GetByUsername(username);

                // Marshal back to UI thread
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (user != null)
                    {
                        // FIX: Create a NEW object to trigger the PropertyChanged event
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
                Application.Current.Dispatcher.Invoke(() =>
                    CurrentUserAccount = new UserAccountModel { DisplayName = "Not logged in" });
            }
        }
    }
}