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
        public const string DefaultAdminPassword = "WPF-Biosun2026";

        public static void SeedDefaultAdmin()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (!string.IsNullOrEmpty(config.OfflineAdminPasswordHash)) return;

            config.OfflineAdminPasswordHash = HashPassword(DefaultAdminPassword);

            var users = new List<OfflineUser>
            {
                new OfflineUser
                {
                    Username = DefaultAdminUser,
                    PasswordHash = HashPassword(DefaultAdminPassword),
                    Role = "Admin"
                },
                new OfflineUser
                {
                    Username = "İsmet AKÇAY",
                    PasswordHash = HashPassword("1234"),
                    Role = "User"
                },
                new OfflineUser
                {
                    Username = "Hüseyin Kara",
                    PasswordHash = HashPassword("1234"),
                    Role = "User"
                },
                new OfflineUser
                {
                    Username = "Misafir",
                    PasswordHash = HashPassword("1234"),
                    Role = "User"
                }
            };
            config.OfflineUsers = JsonConvert.SerializeObject(users, Formatting.Indented);
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
            if (!SaveUserList(users)) return false;

            if (string.Equals(username, DefaultAdminUser, StringComparison.OrdinalIgnoreCase))
            {
                var config = GeneralSettingsManager.Instance.Current;
                config.OfflineAdminPasswordHash = HashPassword(newPassword);
                try { GeneralSettingsManager.Instance.Save(); } catch { }
            }

            return true;
        }

        public static List<OfflineUser> GetUserList()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.OfflineUsers))
                return new List<OfflineUser>();

            try
            {
                return JsonConvert.DeserializeObject<List<OfflineUser>>(config.OfflineUsers)
                    ?? new List<OfflineUser>();
            }
            catch
            {
                return new List<OfflineUser>();
            }
        }

        public static bool SaveUserList(List<OfflineUser> users)
        {
            try
            {
                GeneralSettingsManager.Instance.Current.OfflineUsers =
                    JsonConvert.SerializeObject(users, Formatting.Indented);
                GeneralSettingsManager.Instance.Save();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveUserList failed: {ex.Message}");
                return false;
            }
        }

        public static bool IsDefaultAdminPassword()
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.OfflineAdminPasswordHash)) return true;
            return string.Equals(config.OfflineAdminPasswordHash,
                HashPassword(DefaultAdminPassword), StringComparison.OrdinalIgnoreCase);
        }

        public static bool VerifyAdminPassword(string password)
        {
            var config = GeneralSettingsManager.Instance.Current;
            if (string.IsNullOrEmpty(config.OfflineAdminPasswordHash))
                return password == DefaultAdminPassword;
            return string.Equals(config.OfflineAdminPasswordHash,
                HashPassword(password), StringComparison.OrdinalIgnoreCase);
        }

        public static void ChangeAdminPassword(string oldPassword, string newPassword)
        {
            if (!VerifyAdminPassword(oldPassword))
                throw new UnauthorizedAccessException("Old password is incorrect.");

            var config = GeneralSettingsManager.Instance.Current;
            config.OfflineAdminPasswordHash = HashPassword(newPassword);
            GeneralSettingsManager.Instance.Save();

            var users = GetUserList();
            var adminUser = users.FirstOrDefault(u =>
                string.Equals(u.Username, DefaultAdminUser, StringComparison.OrdinalIgnoreCase));
            if (adminUser != null)
            {
                adminUser.PasswordHash = HashPassword(newPassword);
                SaveUserList(users);
            }
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
