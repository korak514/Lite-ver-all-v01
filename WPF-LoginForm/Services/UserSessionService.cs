using System;

namespace WPF_LoginForm.Services
{
    /// <summary>
    /// A global static service to hold the current user's Role and Name.
    /// This prevents thread-context switching issues associated with Thread.CurrentPrincipal.
    /// </summary>
    public static class UserSessionService
    {
        private static string _currentRole = "Guest";
        private static string _currentUsername = "";

        // --- PUBLIC PROPERTIES (These were missing or private) ---
        public static string CurrentRole => _currentRole;

        public static string CurrentUsername => _currentUsername;

        // Convenient Bool for binding checks
        public static bool IsAdmin => string.Equals(_currentRole, "Admin", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Sets the current session. Should only be called by LoginViewModel.
        /// </summary>
        public static void SetSession(string username, string role)
        {
            _currentUsername = username;

            // Normalize Role Logic
            if (!string.IsNullOrEmpty(role) && role.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                _currentRole = "Admin"; // Force specific casing
            }
            else
            {
                _currentRole = "User"; // Default for non-admins
            }
        }

        /// <summary>
        /// Resets to Guest (Locked state).
        /// </summary>
        public static void Logout()
        {
            _currentRole = "Guest";
            _currentUsername = "";
        }
    }
}