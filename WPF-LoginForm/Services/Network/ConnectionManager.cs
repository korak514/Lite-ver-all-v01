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
        public static async Task<string> ResolveBestHostAsync()
        {
            string primaryIp = Settings.Default.DbHost;
            string backupName = Settings.Default.DbServerName;

            if (string.IsNullOrWhiteSpace(backupName)) return primaryIp;

            if (await PingHostAsync(primaryIp))
            {
                return primaryIp;
            }

            if (await PingHostAsync(backupName))
            {
                UpdateConnectionStrings(backupName);
                return backupName;
            }

            return primaryIp;
        }

        private static void UpdateConnectionStrings(string newHost)
        {
            try
            {
                Settings.Default.DbHost = newHost;

                string port = Settings.Default.DbPort;
                string user = Settings.Default.DbUser;
                bool useWindowsAuth = false;

                string currentPass = "";

                try
                {
                    if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer)
                    {
                        var builder = new SqlConnectionStringBuilder(Settings.Default.SqlAuthConnString);
                        currentPass = builder.Password;
                        useWindowsAuth = builder.IntegratedSecurity;
                    }
                    else
                    {
                        var builder = new NpgsqlConnectionStringBuilder(Settings.Default.PostgresAuthConnString);
                        currentPass = builder.Password;
                    }
                }
                catch
                {
                    currentPass = Settings.Default.DbPassword;
                }

                // --- SQL SERVER BUILDER ---
                var sqlBuilder = new SqlConnectionStringBuilder();

                // FIX: Better Port Handling (Avoid trailing comma)
                if (!string.IsNullOrWhiteSpace(port) && port != "1433")
                    sqlBuilder.DataSource = $"{newHost},{port}";
                else
                    sqlBuilder.DataSource = newHost;

                sqlBuilder.UserID = user;
                sqlBuilder.Password = currentPass;
                sqlBuilder.IntegratedSecurity = useWindowsAuth;
                sqlBuilder.TrustServerCertificate = Settings.Default.TrustServerCertificate;
                sqlBuilder.ConnectTimeout = Settings.Default.ConnectionTimeout;

                sqlBuilder.InitialCatalog = "LoginDb";
                Settings.Default.SqlAuthConnString = sqlBuilder.ConnectionString;

                sqlBuilder.InitialCatalog = "MainDataDb";
                Settings.Default.SqlDataConnString = sqlBuilder.ConnectionString;

                // --- POSTGRES BUILDER ---
                var pgBuilder = new NpgsqlConnectionStringBuilder();
                pgBuilder.Host = newHost;
                if (int.TryParse(port, out int portNum)) pgBuilder.Port = portNum;
                pgBuilder.Username = user;
                pgBuilder.Password = currentPass;
                pgBuilder.TrustServerCertificate = Settings.Default.TrustServerCertificate;
                pgBuilder.Timeout = Settings.Default.ConnectionTimeout;

                pgBuilder.Database = "LoginDb";
                Settings.Default.PostgresAuthConnString = pgBuilder.ConnectionString;

                pgBuilder.Database = "MainDataDb";
                Settings.Default.PostgresDataConnString = pgBuilder.ConnectionString;

                Settings.Default.Save();
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