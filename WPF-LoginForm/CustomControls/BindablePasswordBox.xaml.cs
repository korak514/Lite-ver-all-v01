using System;
using System.Net;
using System.Security;
using System.Windows;
using System.Windows.Controls;

namespace WPF_LoginForm.CustomControls
{
    public partial class BindablePasswordBox : UserControl
    {
        public static readonly DependencyProperty PasswordProperty =
            DependencyProperty.Register("Password", typeof(SecureString), typeof(BindablePasswordBox),
                new PropertyMetadata(null, OnPasswordPropertyChanged));

        public SecureString Password
        {
            get { return (SecureString)GetValue(PasswordProperty); }
            set { SetValue(PasswordProperty, value); }
        }

        public BindablePasswordBox()
        {
            InitializeComponent();
            txtPassword.PasswordChanged += OnPasswordChanged;
        }

        // 1. Handle updates FROM the ViewModel (Loading settings)
        private static void OnPasswordPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BindablePasswordBox passwordBox)
            {
                passwordBox.UpdatePassword();
            }
        }

        private void UpdatePassword()
        {
            if (Password == null)
            {
                if (txtPassword.Password != "") txtPassword.Password = "";
                return;
            }

            // Convert SecureString to normal string to display dots
            // Note: This momentarily exposes the password in memory, which is unavoidable
            // if you want to use the standard PasswordBox for 2-way binding.
            string newPassword = new NetworkCredential("", Password).Password;

            if (txtPassword.Password != newPassword)
            {
                txtPassword.Password = newPassword;
            }
        }

        // 2. Handle updates FROM the UI (Typing)
        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                // Prevent infinite loop by checking if values are already synced
                string currentVmValue = Password == null ? "" : new NetworkCredential("", Password).Password;

                if (txtPassword.Password != currentVmValue)
                {
                    Password = txtPassword.SecurePassword;
                }
            }
        }
    }
}