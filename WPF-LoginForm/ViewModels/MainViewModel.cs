using FontAwesome.Sharp; // Used for icons in UI bindings
using System;
using System.Threading; // Used for Thread.CurrentPrincipal
using System.Windows.Input; // Used for ICommand
using WPF_LoginForm.Models; // Contains UserAccountModel, ViewModelBase
using WPF_LoginForm.Repositories; // Contains IUserRepository, UserRepository, IDataRepository, DataRepository
using WPF_LoginForm.Services; // Contains IDialogService, DialogService, ILogger, FileLogger (or Logging namespace)

namespace WPF_LoginForm.ViewModels
{
    public class MainViewModel : ViewModelBase // Inherits INotifyPropertyChanged implementation
    {
        //Fields
        private UserAccountModel _currentUserAccount;
        private ViewModelBase _currentChildView; // Holds the currently displayed ViewModel (e.g., HomeViewModel, DatarepViewModel)
        private string _caption; // Title displayed in the header
        private IconChar _icon; // Icon displayed in the header
        private readonly IUserRepository _userRepository; // Repository for user-related data access (assuming it's used)

        // --- Services and Repositories needed by Child ViewModels ---
        private readonly IDialogService _dialogService; // Service for showing dialogs
        private readonly IDataRepository _dataRepository; // Repository for general data access
        private readonly ILogger _logger; // <<< FIELD for the logger instance

        // --- Child ViewModel Instances (Cached) ---
        private HomeViewModel _homeViewModel;
        private CustomerViewModel _customerViewModel;
        private DatarepViewModel _datarepViewModel; // Instance for the reports/data view
        private InventoryViewModel _inventoryViewModel;
        // Add other child ViewModel fields if needed

        //Properties (Bindable by the MainView.xaml)
        public UserAccountModel CurrentUserAccount
        {
            get => _currentUserAccount;
            set { _currentUserAccount = value; OnPropertyChanged(); } // Notify UI of changes
        }
        public ViewModelBase CurrentChildView
        {
            get => _currentChildView;
            set { _currentChildView = value; OnPropertyChanged(); } // Notify UI when the main content view changes
        }
        public string Caption
        {
            get => _caption;
            set { _caption = value; OnPropertyChanged(); } // Notify UI when header caption changes
        }
        public IconChar Icon
        {
            get => _icon;
            set { _icon = value; OnPropertyChanged(); } // Notify UI when header icon changes
        }

        //--> Commands (Bindable by Buttons/RadioButtons in MainView.xaml)
        public ICommand ShowHomeViewCommand { get; }
        public ICommand ShowCustomerViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }
        public ICommand ShowInventoryViewCommand { get; }
        // Add ICommand properties for other views like Settings, Help if you implement them

        // --- Constructor ---
        public MainViewModel()
        {
            // <<< Instantiate the logger ONCE >>>
            _logger = new FileLogger("WPF_App_Log"); // Provide a base filename for logs

            // Instantiate other services/repositories, passing the logger if needed
            _dialogService = new DialogService(); // Assuming DialogService doesn't need logging itself
            _userRepository = new UserRepository(); // Assuming UserRepo doesn't need logging for now
            // <<< Pass the logger instance to DataRepository's constructor >>>
            _dataRepository = new DataRepository(_logger);

            CurrentUserAccount = new UserAccountModel();

            // Initialize commands, linking them to their execution methods
            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowCustomerViewCommand = new ViewModelCommand(ExecuteShowCustomerViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand); // This method passes logger now
            ShowInventoryViewCommand = new ViewModelCommand(ExecuteShowInventoryViewCommand);
            // Initialize other commands if added

            // Set the default view to be displayed when the application starts
            ExecuteShowHomeViewCommand(null);

            // Load the details of the currently logged-in user
            LoadCurrentUserData();
            _logger.LogInfo("MainViewModel initialized and default view loaded."); // Log initialization
        }

        // --- Command Execution Methods ---

        // Method executed when the Home/Dashboard menu item is clicked
        private void ExecuteShowHomeViewCommand(object obj)
        {
            _logger.LogInfo("Executing ShowHomeViewCommand.");
            if (_homeViewModel == null)
                _homeViewModel = new HomeViewModel(); // Doesn't need logger currently

            CurrentChildView = _homeViewModel;
            Caption = "Dashboard";
            Icon = IconChar.Home;
        }

        // Method executed when the Customers menu item is clicked
        private void ExecuteShowCustomerViewCommand(object obj)
        {
            _logger.LogInfo("Executing ShowCustomerViewCommand.");
            if (_customerViewModel == null)
                _customerViewModel = new CustomerViewModel(/* Pass logger if needed */);

            CurrentChildView = _customerViewModel;
            Caption = "Customers";
            Icon = IconChar.UserGroup;
        }

        // Method executed when the Inventory menu item is clicked
        private void ExecuteShowInventoryViewCommand(object obj)
        {
            _logger.LogInfo("Executing ShowInventoryViewCommand.");
            if (_inventoryViewModel == null)
                _inventoryViewModel = new InventoryViewModel(/* Pass logger if needed */);

            CurrentChildView = _inventoryViewModel;
            Caption = "Inventory";
            Icon = IconChar.Book;
        }

        // Method executed when the Reports/Data menu item is clicked (Passes Logger)
        private void ExecuteShowReportsViewCommand(object obj)
        {
            _logger.LogInfo("Executing ShowReportsViewCommand.");
            // Reuse instance if it exists, otherwise create new
            if (_datarepViewModel == null)
            {
                _logger.LogInfo("Creating new DatarepViewModel instance.");
                // <<< Pass logger, dialog service, AND data repository >>>
                _datarepViewModel = new DatarepViewModel(_logger, _dialogService, _dataRepository);
            }
            else
            {
                _logger.LogInfo("Reusing existing DatarepViewModel instance.");
            }

            CurrentChildView = _datarepViewModel;
            Caption = "Reports";
            Icon = IconChar.BarChart;
        }

        // Add Execute methods for other views if implemented

        // --- Helper Methods ---

        // Loads user data based on the identity set during login
        private void LoadCurrentUserData()
        {
            try
            {
                _logger.LogInfo("Loading current user data.");
                var identity = Thread.CurrentPrincipal?.Identity;
                if (identity != null && identity.IsAuthenticated)
                {
                    _logger.LogInfo($"Authenticated user: {identity.Name}. Fetching details...");
                    var user = _userRepository.GetByUsername(identity.Name); // Assuming _userRepository field exists and is initialized
                    if (user != null)
                    {
                        CurrentUserAccount.Username = user.Username;
                        CurrentUserAccount.DisplayName = $"{user.Name} {user.LastName}";
                        CurrentUserAccount.ProfilePicture = null;
                        _logger.LogInfo($"User details loaded for {user.Username}.");
                    }
                    else
                    {
                        CurrentUserAccount.DisplayName = $"User '{identity.Name}' not found in repository.";
                        _logger.LogWarning($"User '{identity.Name}' authenticated but not found in repository.");
                    }
                }
                else
                {
                    CurrentUserAccount.DisplayName = "Not logged in / Unauthenticated";
                    _logger.LogInfo("No authenticated user found.");
                }
            }
            catch (Exception ex)
            {
                CurrentUserAccount.DisplayName = "Error loading user data";
                _logger.LogError("Error loading current user data.", ex);
            }
        }
    }
}