using System;
using System.Data;
using System.Data.SqlClient;
using Npgsql;
using WPF_LoginForm.Properties; // Access to Settings

namespace WPF_LoginForm.Services.Database
{
    public static class DbConnectionFactory
    {
        // Helper to get/set the Enum based on the string in Settings
        public static DatabaseType CurrentDatabaseType
        {
            get
            {
                string stored = Settings.Default.DbProvider;
                if (Enum.TryParse(stored, out DatabaseType result))
                    return result;
                return DatabaseType.SqlServer; // Default
            }
            set
            {
                Settings.Default.DbProvider = value.ToString();
                Settings.Default.Save();
            }
        }

        public static IDbConnection GetConnection(ConnectionTarget target)
        {
            string connString;

            switch (CurrentDatabaseType)
            {
                case DatabaseType.PostgreSql:
                    // Read from Settings based on target
                    connString = (target == ConnectionTarget.Auth)
                        ? Settings.Default.PostgresAuthConnString
                        : Settings.Default.PostgresDataConnString;

                    return new NpgsqlConnection(connString);

                case DatabaseType.SqlServer:
                default:
                    // Read from Settings based on target
                    connString = (target == ConnectionTarget.Auth)
                        ? Settings.Default.SqlAuthConnString
                        : Settings.Default.SqlDataConnString;

                    return new SqlConnection(connString);
            }
        }
    }
}