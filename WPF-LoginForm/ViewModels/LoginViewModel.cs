using System;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        //Fields
        private string _username;

        private SecureString _password;
        private string _errorMessage;
        private bool _isViewVisible = true;

        // Mode Flags
        private bool _isReportModeOnly;

        private bool _isSettingsModeOnly;

        private IUserRepository userRepository;
        private readonly ILogger _logger;

        //Properties
        public string Username
        {
            get { return _username; }
            set { _username = value; OnPropertyChanged(nameof(Username)); }
        }

        public SecureString Password
        {
            get { return _password; }
            set { _password = value; OnPropertyChanged(nameof(Password)); }
        }

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public bool IsViewVisible
        {
            get { return _isViewVisible; }
            set { _isViewVisible = value; OnPropertyChanged(nameof(IsViewVisible)); }
        }

        public bool IsReportModeOnly
        {
            get { return _isReportModeOnly; }
            set { _isReportModeOnly = value; OnPropertyChanged(nameof(IsReportModeOnly)); }
        }

        public bool IsSettingsModeOnly
        {
            get { return _isSettingsModeOnly; }
            set { _isSettingsModeOnly = value; OnPropertyChanged(nameof(IsSettingsModeOnly)); }
        }

        //-> Commands
        public ICommand LoginCommand { get; }

        public ICommand RecoverPasswordCommand { get; }
        public ICommand ShowPasswordCommand { get; }
        public ICommand RememberPasswordCommand { get; }
        public ICommand OpenSettingsCommand { get; }

        //Constructor
        public LoginViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Login_Log");
            userRepository = new UserRepository();

            LoginCommand = new ViewModelCommand(ExecuteLoginCommand, CanExecuteLoginCommand);
            RecoverPasswordCommand = new ViewModelCommand(p => ExecuteRecoverPassCommand("", ""));
            OpenSettingsCommand = new ViewModelCommand(ExecuteOpenSettings);
        }

        private bool CanExecuteLoginCommand(object obj)
        {
            return !string.IsNullOrWhiteSpace(Username) && Username.Length >= 3 &&
                   Password != null && Password.Length >= 3;
        }

        private void ExecuteLoginCommand(object obj)
        {
            IsSettingsModeOnly = false;

            var isValidUser = userRepository.AuthenticateUser(new NetworkCredential(Username, Password));
            if (isValidUser)
            {
                // --- NEW: Fetch Role and Set Principal ---
                var user = userRepository.GetByUsername(Username);
                string role = user?.Role ?? "User"; // Default to User if null

                var identity = new GenericIdentity(Username);
                var principal = new GenericPrincipal(identity, new string[] { role });
                Thread.CurrentPrincipal = principal;
                // -----------------------------------------

                IsViewVisible = false; // Closes Login Window
                _logger.LogInfo($"User '{Username}' logged in with Role '{role}'. Report Mode: {IsReportModeOnly}");
            }
            else
            {
                ErrorMessage = "* Invalid username or password";
                _logger.LogWarning($"Failed login attempt: '{Username}'");
            }
        }

        private void ExecuteOpenSettings(object obj)
        {
            IsSettingsModeOnly = true;
            IsViewVisible = false;
            _logger.LogInfo("User bypassed login to access Offline Settings.");
        }

        private void ExecuteRecoverPassCommand(string username, string email)
        {
            throw new NotImplementedException();
        }
    }
}