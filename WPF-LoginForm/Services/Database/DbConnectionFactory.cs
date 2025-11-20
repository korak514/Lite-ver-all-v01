using System;
using System.Data;
using System.Data.SqlClient; // .NET 4.8 SQL Client
using Npgsql;

namespace WPF_LoginForm.Services.Database
{
    public static class DbConnectionFactory
    {
        public static DatabaseType CurrentDatabaseType { get; set; } = DatabaseType.SqlServer;

        // --- CONNECTION STRING 1: AUTH & LOGS ---
        // Contains: [User], [Logs] tables
        private static readonly string _sqlAuthConnectionString = "Server=(local); Database=LoginDb; Integrated Security=true";

        // --- CONNECTION STRING 2: BUSINESS DATA ---
        // Contains: [TestDT], [Customers], [Inventory] tables
        // TODO: Change 'MainDataDb' to the actual name of your data database!
        private static readonly string _sqlDataConnectionString = "Server=(local); Database=MainDataDb; Integrated Security=true";

        // Postgres String (Single DB for now, unless you want to split this too)
        private static readonly string _postgreSqlConnectionString = "Host=localhost; Username=postgres; Password=yourpassword; Database=LoginDb";

        /// <summary>
        /// Creates a connection based on the Provider (SQL/Postgres) and the Target (Auth/Data).
        /// </summary>
        public static IDbConnection GetConnection(ConnectionTarget target)
        {
            switch (CurrentDatabaseType)
            {
                case DatabaseType.PostgreSql:
                    // Currently using one DB for Postgres, but logic can be split here too if needed
                    return new NpgsqlConnection(_postgreSqlConnectionString);

                case DatabaseType.SqlServer:
                default:
                    // Select the correct string based on the target
                    string connString = (target == ConnectionTarget.Auth)
                        ? _sqlAuthConnectionString
                        : _sqlDataConnectionString;

                    return new SqlConnection(connString);
            }
        }
    }
}