using System;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services.Network
{
    public static class ConnectionManager
    {
        /// <summary>
        /// Checks Primary IP vs Backup Name. Updates Settings if a swap is needed.
        /// Returns the working address.
        /// </summary>
        public static async Task<string> ResolveBestHostAsync()
        {
            string primaryIp = Settings.Default.DbHost;
            string backupName = Settings.Default.DbServerName;

            // 1. If no backup exists, we stick with the primary
            if (string.IsNullOrWhiteSpace(backupName)) return primaryIp;

            // 2. Try Primary IP (Fast check - 1s timeout)
            if (await PingHostAsync(primaryIp))
            {
                return primaryIp;
            }

            // 3. Primary failed. Try Backup Name.
            if (await PingHostAsync(backupName))
            {
                // Backup works! We need to update the connection strings to use this new host.
                UpdateConnectionStrings(backupName);
                return backupName;
            }

            // 4. Both failed. Return primary and let the database driver throw the error.
            return primaryIp;
        }

        private static void UpdateConnectionStrings(string newHost)
        {
            try
            {
                // Update the simple setting
                Settings.Default.DbHost = newHost;

                // Read current credentials
                string port = Settings.Default.DbPort;
                string user = Settings.Default.DbUser;
                string pass = Settings.Default.DbPassword;
                bool useWindowsAuth = false; // Assuming false for network scenarios usually

                // Rebuild SQL Server Strings
                var sqlBuilder = new SqlConnectionStringBuilder();
                sqlBuilder.DataSource = newHost + (string.IsNullOrEmpty(port) ? "" : "," + port);
                sqlBuilder.UserID = user;
                sqlBuilder.Password = pass;
                sqlBuilder.IntegratedSecurity = useWindowsAuth;
                sqlBuilder.TrustServerCertificate = Settings.Default.TrustServerCertificate;
                sqlBuilder.ConnectTimeout = Settings.Default.ConnectionTimeout;

                // Update LoginDb String
                sqlBuilder.InitialCatalog = "LoginDb";
                Settings.Default.SqlAuthConnString = sqlBuilder.ConnectionString;

                // Update MainDataDb String
                sqlBuilder.InitialCatalog = "MainDataDb";
                Settings.Default.SqlDataConnString = sqlBuilder.ConnectionString;

                // Rebuild PostgreSQL Strings
                var pgBuilder = new NpgsqlConnectionStringBuilder();
                pgBuilder.Host = newHost;
                if (int.TryParse(port, out int portNum)) pgBuilder.Port = portNum;
                pgBuilder.Username = user;
                pgBuilder.Password = pass;
                pgBuilder.TrustServerCertificate = Settings.Default.TrustServerCertificate;
                pgBuilder.Timeout = Settings.Default.ConnectionTimeout;

                // Update LoginDb String
                pgBuilder.Database = "LoginDb";
                Settings.Default.PostgresAuthConnString = pgBuilder.ConnectionString;

                // Update MainDataDb String
                pgBuilder.Database = "MainDataDb";
                Settings.Default.PostgresDataConnString = pgBuilder.ConnectionString;

                // Save changes to memory (and disk if desired, though memory is enough for session)
                // Settings.Default.Save(); // Uncomment to make the switch permanent
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rebuilding connection strings: {ex.Message}");
            }
        }

        private static async Task<bool> PingHostAsync(string hostOrIp)
        {
            if (string.IsNullOrWhiteSpace(hostOrIp)) return false;
            if (hostOrIp == "." || hostOrIp.ToLower() == "localhost" || hostOrIp == "(local)") return true;

            try
            {
                using (var ping = new Ping())
                {
                    PingReply reply = await ping.SendPingAsync(hostOrIp, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch { return false; }
        }
    }
}