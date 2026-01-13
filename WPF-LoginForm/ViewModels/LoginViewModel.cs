using System;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services; // Ensure this is present
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
                // 1. Reset Session
                UserSessionService.Logout();

                var isValidUser = await userRepository.AuthenticateUserAsync(new NetworkCredential(Username, Password));

                if (isValidUser)
                {
                    var user = userRepository.GetByUsername(Username);

                    // 2. Set Static Session (Single Source of Truth)
                    UserSessionService.SetSession(Username, user?.Role);

                    // 3. Also set Thread Principal for standard .NET compatibility (Optional but good)
                    var identity = new GenericIdentity(Username);
                    var principal = new GenericPrincipal(identity, new string[] { UserSessionService.CurrentRole });
                    Thread.CurrentPrincipal = principal;

                    // 4. Verification
                    if (UserSessionService.CurrentRole == "Guest")
                    {
                        ErrorMessage = "* Error: Access Denied (Role Verification Failed).";
                    }
                    else
                    {
                        _logger.LogInfo($"User '{Username}' Logged In. Session Role: {UserSessionService.CurrentRole}");
                        IsViewVisible = false;
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