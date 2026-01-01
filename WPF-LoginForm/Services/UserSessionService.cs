using System;

namespace WPF_LoginForm.Services
{
    // THIS IS THE SINGLE SOURCE OF TRUTH
    public static class UserSessionService
    {
        // 1. Default Role is ALWAYS Guest
        private static string _currentRole = "Guest";

        // 2. Read-Only Property for the rest of the app
        public static string CurrentRole => _currentRole;

        // 3. Convenience Boolean
        public static bool IsAdmin => string.Equals(_currentRole, "Admin", StringComparison.OrdinalIgnoreCase);

        // 4. The ONLY method to change the role.
        public static void SetSession(string role)
        {
            // Simple normalization
            if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                _currentRole = "Admin";
            }
            else
            {
                _currentRole = role ?? "User";
            }
        }

        public static void Logout()
        {
            _currentRole = "Guest";
        }
    }
}