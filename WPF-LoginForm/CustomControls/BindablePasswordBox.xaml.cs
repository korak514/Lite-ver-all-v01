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

            string newPassword = new NetworkCredential("", Password).Password;

            // FIX: Check equality before assigning to prevent recursive loop
            if (txtPassword.Password != newPassword)
            {
                txtPassword.Password = newPassword;
            }
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (this.DataContext != null)
            {
                // FIX: Check if values are already synced to prevent infinite loop
                string currentVmValue = Password == null ? "" : new NetworkCredential("", Password).Password;

                if (txtPassword.Password != currentVmValue)
                {
                    Password = txtPassword.SecurePassword;
                }
            }
        }
    }
}