// In WPF_LoginForm.ViewModels/MainViewModel.cs
using FontAwesome.Sharp;
using System.Threading;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private UserAccountModel _currentUserAccount;
        private ViewModelBase _currentChildView;
        private string _caption;
        private IconChar _icon;
        private readonly IUserRepository _userRepository;

        private readonly IDialogService _dialogService;
        private readonly IDataRepository _dataRepository;
        private readonly ILogger _logger;

        private HomeViewModel _homeViewModel;
        private CustomerViewModel _customerViewModel;
        private DatarepViewModel _datarepViewModel;
        private InventoryViewModel _inventoryViewModel;

        public UserAccountModel CurrentUserAccount { get => _currentUserAccount; set { _currentUserAccount = value; OnPropertyChanged(); } }
        public ViewModelBase CurrentChildView { get => _currentChildView; set { _currentChildView = value; OnPropertyChanged(); } }
        public string Caption { get => _caption; set { _caption = value; OnPropertyChanged(); } }
        public IconChar Icon { get => _icon; set { _icon = value; OnPropertyChanged(); } }

        public ICommand ShowHomeViewCommand { get; }
        public ICommand ShowCustomerViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }
        public ICommand ShowInventoryViewCommand { get; }

        public MainViewModel()
        {
            _logger = new FileLogger("WPF_App_Log");
            _dialogService = new DialogService();
            _userRepository = new UserRepository();
            _dataRepository = new DataRepository(_logger);

            CurrentUserAccount = new UserAccountModel();

            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);

            ExecuteShowHomeViewCommand(null); // Set the default view
            LoadCurrentUserData();
        }

        private void ExecuteShowHomeViewCommand(object obj)
        {
            if (_homeViewModel == null)
            {
                // MODIFIED: Inject dependencies
                _homeViewModel = new HomeViewModel(_dataRepository, _dialogService);
            }

            _homeViewModel.Activate();

            CurrentChildView = _homeViewModel;
            Caption = "Dashboard";
            Icon = IconChar.Home;
        }

        private void ExecuteShowCustomerViewCommand(object obj)
        {
            if (_customerViewModel == null)
                _customerViewModel = new CustomerViewModel();
            CurrentChildView = _customerViewModel;
            Caption = "Customers";
            Icon = IconChar.UserGroup;
        }

        private void ExecuteShowInventoryViewCommand(object obj)
        {
            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel();
            CurrentChildView = _inventoryViewModel;
            Caption = "Inventory";
            Icon = IconChar.Book;
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
                }
                else
                {
                    CurrentUserAccount.DisplayName = $"User '{identity.Name}' not found.";
                }
            }
            else
            {
                CurrentUserAccount.DisplayName = "Not logged in";
            }
        }
    }
}