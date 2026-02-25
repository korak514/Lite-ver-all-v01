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
        private DashboardPortalViewModel _portalViewModel; // NEW: The Hub

        private HomeViewModel _homeViewModel;              // The actual Dashboard

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

        // --- NEW: Routing to Portal instead of straight to Dashboard ---
        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_portalViewModel == null)
            {
                _portalViewModel = new DashboardPortalViewModel();
                // When a module is clicked, trigger the actual dashboard load
                _portalViewModel.OpenDashboardAction = OnOpenDashboardModule;
            }
            CurrentChildView = _portalViewModel;
            Caption = "Analytics Hub";
            Icon = IconChar.ThLarge;
        }

        // --- NEW: Opening the Specific Dashboard ---
        private void OnOpenDashboardModule(string targetFileName)
        {
            if (_homeViewModel == null)
            {
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService, _logger);
                _homeViewModel.DrillDownRequested += OnDashboardDrillDown;

                // When the user clicks "Go Back" in the dashboard, return to the Hub
                _homeViewModel.ReturnToPortalAction = () => ExecuteShowHomeViewCommand(null);
            }

            CurrentChildView = _homeViewModel;
            Caption = "Dashboard Module";
            Icon = IconChar.ChartPie;

            // Small delay to ensure the View has rendered and Activated before loading file
            Task.Delay(100).ContinueWith(_ => Application.Current.Dispatcher.Invoke(() =>
            {
                _homeViewModel.SelectedDashboardFile = targetFileName;
            }));
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
                // _errorViewModel.DrillDownRequested += OnErrorDrillDown;
            }
            CurrentChildView = _errorViewModel;
            Caption = "Error Analytics";
            Icon = IconChar.PieChart;
        }

        private void ExecuteReturnToLogin(object obj)
        {
            UserSessionService.Logout();
            _logger.LogInfo("User Logged Out.");

            _portalViewModel = null; _homeViewModel = null; _customerViewModel = null; _datarepViewModel = null;
            _inventoryViewModel = null; _settingsViewModel = null; _helpViewModel = null;
            if (_errorViewModel != null) { _errorViewModel = null; }
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