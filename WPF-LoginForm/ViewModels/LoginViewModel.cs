using System;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services; // Required for UserSessionService
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private string _username;
        private SecureString _password;
        private string _errorMessage;
        private bool _isViewVisible = true;
        private bool _isReportModeOnly;
        private bool _isSettingsModeOnly;
        private bool _isBusy;

        private IUserRepository userRepository;
        private readonly ILogger _logger;

        public string Username
        { get => _username; set { _username = value; OnPropertyChanged(); } }

        public SecureString Password
        { get => _password; set { _password = value; OnPropertyChanged(); } }

        public string ErrorMessage
        { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }

        public bool IsViewVisible
        { get => _isViewVisible; set { _isViewVisible = value; OnPropertyChanged(); } }

        public bool IsReportModeOnly
        { get => _isReportModeOnly; set { _isReportModeOnly = value; OnPropertyChanged(); } }

        public bool IsSettingsModeOnly
        { get => _isSettingsModeOnly; set { _isSettingsModeOnly = value; OnPropertyChanged(); } }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                (LoginCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ICommand LoginCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand RecoverPasswordCommand { get; }

        public LoginViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Login_Log");
            userRepository = new UserRepository();
            LoginCommand = new ViewModelCommand(ExecuteLoginCommand, CanExecuteLoginCommand);
            OpenSettingsCommand = new ViewModelCommand(ExecuteOpenSettings);
            RecoverPasswordCommand = new ViewModelCommand(p => { });
        }

        private bool CanExecuteLoginCommand(object obj)
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(Username) && Username.Length >= 3 && Password != null && Password.Length >= 3;
        }

        private async void ExecuteLoginCommand(object obj)
        {
            IsBusy = true;
            ErrorMessage = "";
            IsSettingsModeOnly = false;

            try
            {
                // 1. Ensure Session is Guest (Locked) before starting
                UserSessionService.Logout();

                // 2. Authenticate Credentials (Async)
                bool isValidCreds = await userRepository.AuthenticateUserAsync(new NetworkCredential(Username, Password));

                if (isValidCreds)
                {
                    // 3. Get User Details to find Role
                    var user = await Task.Run(() => userRepository.GetByUsername(Username));
                    string dbRole = user?.Role;

                    // 4. Set the Global Session
                    // The Service handles the logic: if dbRole == "admin" (any case) -> it becomes "Admin"
                    UserSessionService.SetSession(Username, dbRole);

                    // 5. Final Security Check
                    if (UserSessionService.CurrentRole == "Guest")
                    {
                        ErrorMessage = "* Access Denied: Unable to verify role.";
                        _logger.LogWarning($"User {Username} auth success, but Role remained Guest.");
                    }
                    else
                    {
                        _logger.LogInfo($"User '{Username}' Logged In. Session Role: {UserSessionService.CurrentRole}");
                        IsViewVisible = false; // Close Login Window
                    }
                }
                else
                {
                    ErrorMessage = "* Invalid username or password";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"* Error: {ex.Message}";
                _logger.LogError("Login failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ExecuteOpenSettings(object obj)
        {
            IsSettingsModeOnly = true;
            IsViewVisible = false;
        }
    }
}