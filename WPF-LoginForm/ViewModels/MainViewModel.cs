using FontAwesome.Sharp;
using System.Threading;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        // Fields
        private UserAccountModel _currentUserAccount;
        private ViewModelBase _currentChildView;
        private string _caption;
        private IconChar _icon;
        private readonly IUserRepository _userRepository;

        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        // Child ViewModels
        private HomeViewModel _homeViewModel;
        private CustomerViewModel _customerViewModel;
        private DatarepViewModel _datarepViewModel;
        private InventoryViewModel _inventoryViewModel;
        private SettingsViewModel _settingsViewModel;
        private HelpViewModel _helpViewModel;

        // Properties
        public UserAccountModel CurrentUserAccount
        {
            get => _currentUserAccount;
            set { _currentUserAccount = value; OnPropertyChanged(); }
        }

        public ViewModelBase CurrentChildView
        {
            get => _currentChildView;
            set { _currentChildView = value; OnPropertyChanged(); }
        }

        public string Caption
        {
            get => _caption;
            set { _caption = value; OnPropertyChanged(); }
        }

        public IconChar Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand ShowHomeViewCommand { get; }
        public ICommand ShowCustomerViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }
        public ICommand ShowInventoryViewCommand { get; }
        public ICommand ShowSettingsViewCommand { get; }
        public ICommand ShowHelpViewCommand { get; }

        // Constructor
        public MainViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Fallback_Log");

            _dialogService = new DialogService();
            _userRepository = new UserRepository();
            _dataRepository = new DataRepository(_logger);

            CurrentUserAccount = new UserAccountModel();

            // Initialize Commands
            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowHelpViewCommand = new ViewModelCommand(ExecuteShowHelpViewCommand);

            // Default View
            ExecuteShowHomeViewCommand(null);
            LoadCurrentUserData();

            _logger.LogInfo("Main Dashboard initialized.");
        }

        // Command Execution Methods
        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_homeViewModel == null)
            {
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService);
            }

            _homeViewModel.Activate();

            CurrentChildView = _homeViewModel;
            Caption = "Dashboard";
            Icon = IconChar.Home;
            _logger.LogInfo("Navigated to Dashboard.");
        }

        private void ExecuteShowCustomerViewCommand(object obj)
        {
            if (_customerViewModel == null)
                _customerViewModel = new CustomerViewModel();

            CurrentChildView = _customerViewModel;
            Caption = "Customers";
            Icon = IconChar.UserGroup;
            _logger.LogInfo("Navigated to Customers View.");
        }

        // --- UPDATED: Pass Dependencies to InventoryViewModel ---
        private void ExecuteShowInventoryViewCommand(object obj)
        {
            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel(_dataRepository, _dialogService);

            CurrentChildView = _inventoryViewModel;
            Caption = "System Logs"; // Updated Caption
            Icon = IconChar.ListAlt;   // Updated Icon
            _logger.LogInfo("Navigated to System Logs View.");
        }

        private void ExecuteShowReportsViewCommand(object obj)
        {
            if (_datarepViewModel == null)
            {
                _datarepViewModel = new DatarepViewModel(_logger, _dialogService, _dataRepository);
            }

            CurrentChildView = _datarepViewModel;
            Caption = "Reports";
            Icon = IconChar.BarChart;
            _logger.LogInfo("Navigated to Reports View.");
        }

        private void ExecuteShowSettingsViewCommand(object obj)
        {
            if (_settingsViewModel == null)
                _settingsViewModel = new SettingsViewModel();

            CurrentChildView = _settingsViewModel;
            Caption = "Settings";
            Icon = IconChar.Cogs;
            _logger.LogInfo("Navigated to Settings View.");
        }

        private void ExecuteShowHelpViewCommand(object obj)
        {
            if (_helpViewModel == null)
                _helpViewModel = new HelpViewModel();

            CurrentChildView = _helpViewModel;
            Caption = "Help";
            Icon = IconChar.QuestionCircle;
            _logger.LogInfo("Navigated to Help View.");
        }

        private void LoadCurrentUserData()
        {
            var identity = Thread.CurrentPrincipal?.Identity;
            if (identity != null && identity.IsAuthenticated)
            {
                var user = _userRepository.GetByUsername(identity.Name);
                if (user != null)
                {
                    CurrentUserAccount.Username = user.Username;
                    CurrentUserAccount.DisplayName = $"{user.Name} {user.LastName}";
                    CurrentUserAccount.ProfilePicture = null;
                    _logger.LogInfo($"User loaded: {user.Username}");
                }
                else
                {
                    CurrentUserAccount.DisplayName = "Unknown User";
                    _logger.LogWarning($"User data not found for: {identity.Name}");
                }
            }
            else
            {
                CurrentUserAccount.DisplayName = "Not logged in";
            }
        }
    }
}