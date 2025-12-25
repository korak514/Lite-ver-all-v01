using FontAwesome.Sharp;
using System.Threading;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using System;

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
            CurrentUserAccount = new UserAccountModel();

            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowHelpViewCommand = new ViewModelCommand(ExecuteShowHelpViewCommand);
            ReturnToLoginCommand = new ViewModelCommand(ExecuteReturnToLogin);

            LoadCurrentUserData();
            _logger.LogInfo("Main Dashboard initialized.");
        }

        public void Initialize(AppMode mode)
        {
            if (mode == AppMode.SettingsOnly) { IsNavigationVisible = false; IsOfflineMode = true; ExecuteShowSettingsViewCommand(null); _logger.LogInfo("Started in 'Settings Only' Mode."); CurrentUserAccount.DisplayName = "Offline Config"; }
            else if (mode == AppMode.ReportOnly) { IsNavigationVisible = false; IsOfflineMode = false; ExecuteShowReportsViewCommand(null); _logger.LogInfo("Started in 'Only Report' Mode."); }
            else { IsNavigationVisible = true; IsOfflineMode = false; ExecuteShowHomeViewCommand(null); _logger.LogInfo("Started in Normal Mode."); }
        }

        private void ExecuteReturnToLogin(object obj)
        { var filename = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName; System.Diagnostics.Process.Start(filename); System.Windows.Application.Current.Shutdown(); }

        private void DeactivateCurrentView()
        { if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Deactivate(); }

        private void ActivateCurrentView()
        { if (_currentChildView is HomeViewModel homeViewModel) homeViewModel.Activate(); }

        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_homeViewModel == null)
            {
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService, _logger);
                // Subscribe to Drill Down
                _homeViewModel.DrillDownRequested += OnDashboardDrillDown;
            }
            CurrentChildView = _homeViewModel; Caption = "Dashboard"; Icon = IconChar.Home;
        }

        // --- UPDATED: 3-Argument Handler ---
        private void OnDashboardDrillDown(string tableName, DateTime start, DateTime end)
        {
            ExecuteShowReportsViewCommand(null);
            if (_datarepViewModel != null)
            {
                _datarepViewModel.LoadTableWithFilter(tableName, start, end);
            }
        }

        // -----------------------------------

        private void ExecuteShowCustomerViewCommand(object obj)
        { if (_customerViewModel == null) _customerViewModel = new CustomerViewModel(_dataRepository); CurrentChildView = _customerViewModel; Caption = "Customers"; Icon = IconChar.UserGroup; }

        private void ExecuteShowInventoryViewCommand(object obj)
        { if (_inventoryViewModel == null) _inventoryViewModel = new InventoryViewModel(_dataRepository, _dialogService); CurrentChildView = _inventoryViewModel; Caption = "System Logs"; Icon = IconChar.ListAlt; }

        private void ExecuteShowReportsViewCommand(object obj)
        { if (_datarepViewModel == null) _datarepViewModel = new DatarepViewModel(_logger, _dialogService, _dataRepository); CurrentChildView = _datarepViewModel; Caption = "Reports"; Icon = IconChar.BarChart; }

        private void ExecuteShowSettingsViewCommand(object obj)
        { if (_settingsViewModel == null) _settingsViewModel = new SettingsViewModel(); CurrentChildView = _settingsViewModel; Caption = "Settings"; Icon = IconChar.Cogs; }

        private void ExecuteShowHelpViewCommand(object obj)
        { if (_helpViewModel == null) _helpViewModel = new HelpViewModel(); CurrentChildView = _helpViewModel; Caption = "Help"; Icon = IconChar.QuestionCircle; }

        private void LoadCurrentUserData()
        {
            var identity = Thread.CurrentPrincipal?.Identity;
            if (identity != null && identity.IsAuthenticated)
            {
                var user = _userRepository.GetByUsername(identity.Name);
                if (user != null) { CurrentUserAccount.Username = user.Username; CurrentUserAccount.DisplayName = $"{user.Name} {user.LastName}"; CurrentUserAccount.Role = user.Role; CurrentUserAccount.ProfilePicture = null; }
                else { CurrentUserAccount.DisplayName = "Unknown User"; }
            }
            else { CurrentUserAccount.DisplayName = "Not logged in"; }
        }
    }
}