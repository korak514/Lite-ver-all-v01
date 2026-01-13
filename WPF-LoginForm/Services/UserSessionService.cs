using System;

namespace WPF_LoginForm.Services
{
    public static class UserSessionService
    {
        private static string _currentRole = "Guest";
        private static string _currentUsername = "";

        public static string CurrentRole => _currentRole;
        public static string CurrentUsername => _currentUsername;

        // The crucial check used by Settings and Inventory
        public static bool IsAdmin => string.Equals(_currentRole, "Admin", StringComparison.OrdinalIgnoreCase);

        public static void SetSession(string username, string role)
        {
            _currentUsername = username;

            // Force Normalize logic here
            if (!string.IsNullOrEmpty(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _currentRole = "Admin";
            }
            else
            {
                _currentRole = role?.Trim() ?? "User";
            }
        }

        public static void Logout()
        {
            _currentRole = "Guest";
            _currentUsername = "";
        }
    }
}