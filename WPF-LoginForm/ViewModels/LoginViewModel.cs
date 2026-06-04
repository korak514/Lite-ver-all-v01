// ViewModels/LoginViewModel.cs
using System;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.Properties;

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

        // --- CHANGED: Default is FALSE. App is Offline by default. ---
        // Binding this to the CheckBox allows the user to "Go Online".
        private bool _isOnlineMode = false;

        private bool _isBusy;

        private IUserRepository userRepository;
        private readonly ILogger _logger;

        public string Username
        {
            get => _username;
            set
            {
                _username = value;
                OnPropertyChanged();
                (LoginCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

        public SecureString Password
        {
            get => _password;
            set
            {
                if (_password != value)
                {
                    _password?.Dispose();
                    _password = value;
                    OnPropertyChanged();
                    (LoginCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsViewVisible
        {
            get => _isViewVisible;
            set { _isViewVisible = value; OnPropertyChanged(); }
        }

        public bool IsReportModeOnly
        {
            get => _isReportModeOnly;
            set { _isReportModeOnly = value; OnPropertyChanged(); }
        }

        public bool IsSettingsModeOnly
        {
            get => _isSettingsModeOnly;
            set { _isSettingsModeOnly = value; OnPropertyChanged(); }
        }

        // --- CHANGED: Renamed property to reflect the new "Go Online" logic ---
        public bool IsOnlineMode
        {
            get => _isOnlineMode;
            set
            {
                _isOnlineMode = value;
                OnPropertyChanged();
                // Re-evaluate if the login button should be clickable (validation requirements change)
                (LoginCommand as ViewModelCommand)?.RaiseCanExecuteChanged();
            }
        }

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
        public ICommand ChangePasswordCommand { get; }

        public LoginViewModel()
        {
            _logger = App.GlobalLogger ?? new FileLogger("Login_Log");
            userRepository = new UserRepository();
            LoginCommand = new ViewModelCommand(ExecuteLoginCommand, CanExecuteLoginCommand);
            OpenSettingsCommand = new ViewModelCommand(ExecuteOpenSettings, (o) => !IsBusy);
            ChangePasswordCommand = new ViewModelCommand(ExecuteChangePassword);
        }

        private bool CanExecuteLoginCommand(object obj)
        {
            // If we are NOT in Online Mode (Offline), require Username and Password
            if (!IsOnlineMode)
                return !IsBusy && !string.IsNullOrWhiteSpace(Username) && Username.Length >= 1 && Password != null && Password.Length >= 1;

            // If we ARE in Online Mode, require Username and Password
            return !IsBusy && !string.IsNullOrWhiteSpace(Username) && Username.Length >= 3 && Password != null && Password.Length >= 3;
        }

        private async void ExecuteLoginCommand(object obj)
        {
            IsBusy = true;
            ErrorMessage = "";
            IsSettingsModeOnly = false;

            try
            {
                // 1. Offline Mode Logic
                // Validate against encrypted offline user store
                if (!IsOnlineMode)
                {
                    // Reload config from shared file so user changes from other PCs are visible
                    GeneralSettingsManager.Instance.Load();

                    UserSessionService.Logout();
                    string pw = new NetworkCredential("", Password).Password;
                    if (OfflineUserStore.Authenticate(Username, pw))
                    {
                        UserSessionService.SetSession(Username, OfflineUserStore.IsAdminUser(Username) ? "Admin" : "User");
                        _logger.LogInfo($"User '{Username}' logged in (Offline Mode).");
                        IsViewVisible = false;
                    }
                    else
                    {
                        ErrorMessage = Resources.Msg_InvalidCredentials;
                    }
                    return;
                }

                // 2. Online Mode Logic
                UserSessionService.Logout();

                var isValidUser = await userRepository.AuthenticateUserAsync(new NetworkCredential(Username, Password));

                if (isValidUser)
                {
                    var user = userRepository.GetByUsername(Username);
                    UserSessionService.SetSession(Username, user?.Role);

                    var identity = new GenericIdentity(Username);
                    var principal = new GenericPrincipal(identity, new string[] { UserSessionService.CurrentRole });
                    Thread.CurrentPrincipal = principal;

                    if (UserSessionService.CurrentRole == "Guest")
                    {
                        ErrorMessage = Resources.Msg_AccessDeniedRole;
                    }
                    else
                    {
                        _logger.LogInfo($"User '{Username}' Logged In. Session Role: {UserSessionService.CurrentRole}");
                        IsViewVisible = false;
                    }
                }
                else
                {
                    ErrorMessage = Resources.Msg_InvalidCredentials;
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

        private void ExecuteChangePassword(object obj)
        {
            try
            {
                var changePwWindow = new Views.PasswordChangeView(IsOnlineMode, Username);
                changePwWindow.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                changePwWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                string msg = $"PasswordChangeView açılırken hata:\n\n{ex.GetType().Name}: {ex.Message}\n\n";
                Exception inner = ex.InnerException;
                int depth = 0;
                while (inner != null && depth < 5)
                {
                    msg += $"--- Inner {depth}: {inner.GetType().Name}: {inner.Message}\n";
                    inner = inner.InnerException;
                    depth++;
                }
                msg += $"\nStack:\n{ex.StackTrace}";
                MessageBox.Show(msg, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}