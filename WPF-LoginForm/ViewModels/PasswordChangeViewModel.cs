// ViewModels/PasswordChangeViewModel.cs
using System;
using System.Linq;
using System.Net;
using System.Security;
using System.Windows;
using System.Windows.Input;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;

namespace WPF_LoginForm.ViewModels
{
    public class PasswordChangeViewModel : ViewModelBase
    {
        private SecureString _oldPassword;
        private SecureString _newPassword;
        private SecureString _confirmPassword;
        private string _errorMessage;
        private bool _isOnline;
        private string _username;

        public SecureString OldPassword
        {
            get => _oldPassword;
            set { _oldPassword = value; OnPropertyChanged(); (ChangePasswordCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        }

        public SecureString NewPassword
        {
            get => _newPassword;
            set { _newPassword = value; OnPropertyChanged(); (ChangePasswordCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        }

        public SecureString ConfirmPassword
        {
            get => _confirmPassword;
            set { _confirmPassword = value; OnPropertyChanged(); (ChangePasswordCommand as ViewModelCommand)?.RaiseCanExecuteChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        public ICommand ChangePasswordCommand { get; }
        public ICommand CancelCommand { get; }

        public PasswordChangeViewModel() : this(false, "admin")
        {
        }

        public PasswordChangeViewModel(bool isOnline, string currentUsername)
        {
            _isOnline = isOnline;
            _username = currentUsername;

            ChangePasswordCommand = new ViewModelCommand(ExecuteChangePassword, CanExecuteChangePassword);
            CancelCommand = new ViewModelCommand(p =>
            {
                foreach (Window w in Application.Current.Windows)
                {
                    if (w is Views.PasswordChangeView) { w.Close(); break; }
                }
            });
        }

        private bool CanExecuteChangePassword(object obj)
        {
            return OldPassword != null && OldPassword.Length >= 1
                && NewPassword != null && NewPassword.Length >= 1
                && ConfirmPassword != null && ConfirmPassword.Length >= 1;
        }

        private async void ExecuteChangePassword(object obj)
        {
            ErrorMessage = "";

            string oldPw = new NetworkCredential("", OldPassword).Password;
            string newPw = new NetworkCredential("", NewPassword).Password;
            string confirmPw = new NetworkCredential("", ConfirmPassword).Password;

            // Validate new password
            string validationError = ValidatePassword(newPw);
            if (validationError != null)
            {
                ErrorMessage = validationError;
                return;
            }

            // Check new passwords match
            if (newPw != confirmPw)
            {
                ErrorMessage = Resources.Str_PasswordMismatch;
                return;
            }

            try
            {
                if (_isOnline)
                {
                    // Online mode: use UserRepository to change password
                    var userRepo = new Repositories.UserRepository();
                    var credential = new NetworkCredential(_username, oldPw);
                    bool valid = await userRepo.AuthenticateUserAsync(credential);
                    if (!valid)
                    {
                        ErrorMessage = Resources.Str_OldPasswordWrong;
                        return;
                    }
                    bool changed = await userRepo.ChangePasswordAsync(_username, oldPw, newPw);
                    if (changed)
                    {
                        MessageBox.Show(Resources.Str_PasswordChanged, Resources.ForgotPassword,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseWindow();
                    }
                    else
                    {
                        ErrorMessage = Resources.Str_OldPasswordWrong;
                    }
                }
                else
                {
                    // Offline mode: use OfflineUserStore
                    bool changed = OfflineUserStore.ChangePassword(_username, oldPw, newPw);
                    if (changed)
                    {
                        MessageBox.Show(Resources.Str_PasswordChanged, Resources.ForgotPassword,
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        CloseWindow();
                    }
                    else
                    {
                        ErrorMessage = Resources.Str_OldPasswordWrong;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"{Resources.Str_Error}: {ex.Message}";
            }
        }

        private string ValidatePassword(string password)
        {
            if (password.Length < 6)
                return Resources.Str_InvalidPasswordFormat;

            string[] simplePatterns = {
                "123456", "111111", "222222", "333333", "444444", "555555",
                "666666", "777777", "888888", "999999", "000000",
                "abc123", "qwerty", "password", "654321", "abcdef", "aaaaaa",
                "asdfgh", "zxcvbn", "1qaz2w", "qwerty123", "123456789"
            };

            string lower = password.ToLowerInvariant();
            if (simplePatterns.Any(p => lower == p || lower.Contains(p)))
                return Resources.Str_InvalidPasswordFormat;

            return null;
        }

        private void CloseWindow()
        {
            foreach (Window w in Application.Current.Windows)
            {
                if (w is Views.PasswordChangeView) { w.Close(); break; }
            }
        }
    }
}
