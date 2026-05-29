// Services/OfflineUserStore.cs
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Services
{
    public static class OfflineUserStore
    {
        private const string DefaultAdminUser = "admin";
        private const string DefaultAdminPassword = "WPF-Biosun2026";

        public static void SeedDefaultAdmin()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (!string.IsNullOrEmpty(config.EncryptedOfflineAdminPassword)) return;

            string protectedPw = OfflineDataEncryption.ProtectPassword(DefaultAdminPassword);
            config.EncryptedOfflineAdminPassword = protectedPw;

            var users = new List<OfflineUser>
            {
                new OfflineUser
                {
                    Username = DefaultAdminUser,
                    PasswordHash = HashPassword(DefaultAdminPassword),
                    Role = "Admin"
                }
            };
            config.EncryptedOfflineUsers = EncryptUserList(users);
            GeneralSettingsManager.Instance.Save();
        }

        public static bool Authenticate(string username, string password)
        {
            var users = GetUserList();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user == null) return false;

            return string.Equals(user.PasswordHash, HashPassword(password), StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAdminUser(string username)
        {
            var users = GetUserList();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            return user?.Role == "Admin";
        }

        public static bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            var users = GetUserList();
            var user = users.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            if (user == null) return false;
            if (!string.Equals(user.PasswordHash, HashPassword(oldPassword), StringComparison.OrdinalIgnoreCase))
                return false;

            user.PasswordHash = HashPassword(newPassword);
            SaveUserList(users);

            // If this is the admin user, also update the DPAPI-protected admin password field
            if (string.Equals(username, DefaultAdminUser, StringComparison.OrdinalIgnoreCase))
            {
                var config = GeneralSettingsManager.Instance.Current;
                config.EncryptedOfflineAdminPassword = OfflineDataEncryption.ProtectPassword(newPassword);
                GeneralSettingsManager.Instance.Save();
            }

            return true;
        }

        public static List<OfflineUser> GetUserList()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.EncryptedOfflineUsers))
                return new List<OfflineUser>();

            try
            {
                string json = OfflineDataEncryption.UnprotectPassword(config.EncryptedOfflineUsers);
                return JsonConvert.DeserializeObject<List<OfflineUser>>(json) ?? new List<OfflineUser>();
            }
            catch
            {
                return new List<OfflineUser>();
            }
        }

        public static void SaveUserList(List<OfflineUser> users)
        {
            string json = JsonConvert.SerializeObject(users, Formatting.Indented);
            string protectedJson = OfflineDataEncryption.ProtectPassword(json);
            GeneralSettingsManager.Instance.Current.EncryptedOfflineUsers = protectedJson;
            GeneralSettingsManager.Instance.Save();
        }

        public static bool IsDefaultAdminPassword()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.EncryptedOfflineAdminPassword)) return true;

            try
            {
                string currentPw = OfflineDataEncryption.UnprotectPassword(config.EncryptedOfflineAdminPassword);
                return currentPw == DefaultAdminPassword;
            }
            catch
            {
                return true;
            }
        }

        public static bool VerifyAdminPassword(string password)
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.EncryptedOfflineAdminPassword)) return password == DefaultAdminPassword;

            try
            {
                string storedPw = OfflineDataEncryption.UnprotectPassword(config.EncryptedOfflineAdminPassword);
                return storedPw == password;
            }
            catch
            {
                return password == DefaultAdminPassword;
            }
        }

        public static void ChangeAdminPassword(string oldPassword, string newPassword)
        {
            if (!VerifyAdminPassword(oldPassword))
                throw new UnauthorizedAccessException("Old password is incorrect.");

            var config = GeneralSettingsManager.Instance.Current;
            config.EncryptedOfflineAdminPassword = OfflineDataEncryption.ProtectPassword(newPassword);
            GeneralSettingsManager.Instance.Save();

            // Also update the admin user's password hash in the user list
            var users = GetUserList();
            var adminUser = users.FirstOrDefault(u =>
                string.Equals(u.Username, DefaultAdminUser, StringComparison.OrdinalIgnoreCase));
            if (adminUser != null)
            {
                adminUser.PasswordHash = HashPassword(newPassword);
                SaveUserList(users);
            }
        }

        private static string EncryptUserList(List<OfflineUser> users)
        {
            string json = JsonConvert.SerializeObject(users, Formatting.Indented);
            return OfflineDataEncryption.ProtectPassword(json);
        }

        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                var sb = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                    sb.Append(bytes[i].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
