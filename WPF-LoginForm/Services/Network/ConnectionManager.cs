using System;
using System.Data.SqlClient;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Properties;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;

namespace WPF_LoginForm.Services.Network
{
    public static class ConnectionManager
    {
        public static async Task<string> ResolveBestHostAsync()
        {
            var config = GeneralSettingsManager.Instance.Current;
            string primaryIp = config.DbHost;
            string backupName = config.DbServerName;

            // If no backup set, just return primary.
            if (string.IsNullOrWhiteSpace(backupName)) return primaryIp;

            // 1. Try Backup (Computer Name) first if it's set
            // Rationale: In a dynamic DHCP environment, name is often more reliable than static IP config.
            if (await PingHostAsync(backupName))
            {
                if (backupName != primaryIp)
                {
                    UpdateConnectionStrings(backupName);
                }
                return backupName;
            }

            // 2. Fallback to Primary IP
            if (await PingHostAsync(primaryIp))
            {
                return primaryIp;
            }

            // If neither works, default to primary logic and let the DB driver throw the specific timeout error
            return primaryIp;
        }

        private static void UpdateConnectionStrings(string newHost)
        {
            try
            {
                var config = GeneralSettingsManager.Instance.Current;
                config.DbHost = newHost;

                string port = config.DbPort;
                string user = config.DbUser;
                bool useWindowsAuth = false;
                string currentPass = "";

                // Retrieve current password/auth method securely from builder if possible
                try
                {
                    if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer)
                    {
                        var builder = new SqlConnectionStringBuilder(config.SqlAuthConnString);
                        currentPass = builder.Password;
                        useWindowsAuth = builder.IntegratedSecurity;
                    }
                    else
                    {
                        var builder = new NpgsqlConnectionStringBuilder(config.PostgresAuthConnString);
                        currentPass = builder.Password;
                    }
                }
                catch
                {
                    // Fallback if connection string parsing fails
                    currentPass = "";
                }

                // --- SQL SERVER REBUILD ---
                var sqlBuilder = new SqlConnectionStringBuilder();

                // Handle non-standard ports for SQL Server (Format: "Host,Port")
                if (!string.IsNullOrWhiteSpace(port) && port != "1433")
                    sqlBuilder.DataSource = $"{newHost},{port}";
                else
                    sqlBuilder.DataSource = newHost;

                sqlBuilder.UserID = user;
                sqlBuilder.Password = currentPass;
                sqlBuilder.IntegratedSecurity = useWindowsAuth;
                sqlBuilder.TrustServerCertificate = config.TrustServerCertificate;
                sqlBuilder.ConnectTimeout = config.ConnectionTimeout;
                sqlBuilder.PersistSecurityInfo = true;

                sqlBuilder.InitialCatalog = "LoginDb";
                config.SqlAuthConnString = sqlBuilder.ConnectionString;

                sqlBuilder.InitialCatalog = "MainDataDb";
                config.SqlDataConnString = sqlBuilder.ConnectionString;

                // --- POSTGRES REBUILD ---
                var pgBuilder = new NpgsqlConnectionStringBuilder();
                pgBuilder.Host = newHost;
                if (int.TryParse(port, out int portNum)) pgBuilder.Port = portNum;
                pgBuilder.Username = user;
                pgBuilder.Password = currentPass;
                pgBuilder.TrustServerCertificate = config.TrustServerCertificate;
                pgBuilder.Timeout = config.ConnectionTimeout;
                pgBuilder.PersistSecurityInfo = true;

                pgBuilder.Database = "LoginDb";
                config.PostgresAuthConnString = pgBuilder.ConnectionString;

                pgBuilder.Database = "MainDataDb";
                config.PostgresDataConnString = pgBuilder.ConnectionString;

                GeneralSettingsManager.Instance.Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rebuilding connection strings: {ex.Message}");
            }
        }

        private static async Task<bool> PingHostAsync(string hostOrIp)
        {
            if (string.IsNullOrWhiteSpace(hostOrIp)) return false;

            // Bypass Ping for localhost shortcuts
            if (hostOrIp == "." || hostOrIp.ToLower() == "localhost" || hostOrIp == "(local)" || hostOrIp == "127.0.0.1") return true;

            try
            {
                using (var ping = new Ping())
                {
                    // Reduced timeout to 1000ms (1 second) to prevent long startup delays
                    PingReply reply = await ping.SendPingAsync(hostOrIp, 1000);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}