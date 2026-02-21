// Services/Database/DatabaseBootstrapper.cs
using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using Npgsql;
using WPF_LoginForm.Properties;

namespace WPF_LoginForm.Services.Database
{
    public static class DatabaseBootstrapper
    {
        private enum DbStatus
        { Unreachable, MissingAuthDb, MissingDataDb, MissingSchema, Ready }

        private static string _sqlErrorDetails = "";
        private static string _pgErrorDetails = "";

        public static bool Run()
        {
            var sqlStatus = CheckStatus(DatabaseType.SqlServer, out _sqlErrorDetails);
            var pgStatus = CheckStatus(DatabaseType.PostgreSql, out _pgErrorDetails);

            if (sqlStatus == DbStatus.Ready && pgStatus == DbStatus.Ready)
            {
                PerformMigrations(DbConnectionFactory.CurrentDatabaseType);
                return true;
            }

            if (sqlStatus == DbStatus.MissingAuthDb || sqlStatus == DbStatus.MissingDataDb || sqlStatus == DbStatus.MissingSchema)
            {
                if (AskToInitialize("SQL Server", sqlStatus != DbStatus.MissingSchema))
                    return InitializeAndSet(DatabaseType.SqlServer, true);
                return false;
            }

            if (pgStatus == DbStatus.MissingAuthDb || pgStatus == DbStatus.MissingDataDb || pgStatus == DbStatus.MissingSchema)
            {
                if (AskToInitialize("PostgreSQL", pgStatus != DbStatus.MissingSchema))
                    return InitializeAndSet(DatabaseType.PostgreSql, true);
                return false;
            }

            if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer && sqlStatus == DbStatus.Ready)
            {
                PerformMigrations(DatabaseType.SqlServer);
                return true;
            }
            if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql && pgStatus == DbStatus.Ready)
            {
                PerformMigrations(DatabaseType.PostgreSql);
                return true;
            }

            return false;
        }

        private static void PerformMigrations(DatabaseType type)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    // 1. Ensure 'Role' column exists
                    string roleQuery = type == DatabaseType.SqlServer
                        ? "SELECT 1 FROM sys.columns WHERE Name = 'Role' AND Object_ID = Object_ID('User')"
                        : "SELECT 1 FROM information_schema.columns WHERE table_name='User' AND column_name='Role'";

                    if (ExecuteScalar(conn, roleQuery) == 0)
                    {
                        string addCol = type == DatabaseType.SqlServer
                            ? "ALTER TABLE [User] ADD [Role] NVARCHAR(50) DEFAULT 'User' WITH VALUES"
                            : "ALTER TABLE \"User\" ADD COLUMN \"Role\" VARCHAR(50) DEFAULT 'User'";
                        ExecuteNonQuery(conn, addCol);
                    }

                    // 2. Ensure Admin Role
                    string updateRole = type == DatabaseType.SqlServer
                        ? "UPDATE [User] SET [Role] = 'Admin' WHERE LOWER([Username]) = 'admin'"
                        : "UPDATE \"User\" SET \"Role\" = 'Admin' WHERE LOWER(\"Username\") = 'admin'";
                    ExecuteNonQuery(conn, updateRole);

                    // 3. OPTIONAL: Force reset Admin password if you are completely locked out.
                    // Uncommenting this line will reset admin password to 'admin' (Hashed) on every startup.
                    // string adminHash = WPF_LoginForm.Services.PasswordHelper.HashPassword("admin");
                    // string updatePass = type == DatabaseType.SqlServer
                    //    ? $"UPDATE [User] SET [Password] = '{adminHash}' WHERE LOWER([Username]) = 'admin'"
                    //    : $"UPDATE \"User\" SET \"Password\" = '{adminHash}' WHERE LOWER(\"Username\") = 'admin'";
                    // ExecuteNonQuery(conn, updatePass);
                }
            }
            catch { }
        }

        private static bool AskToInitialize(string providerName, bool needsDbCreation)
        {
            string task = needsDbCreation ? "Create Databases (LoginDb & MainDataDb)" : "Create Missing Tables";
            var result = MessageBox.Show($"{providerName} connected.\nStatus: {task} required.\n\nProceed?",
                "Database Initialization", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        }

        private static bool InitializeAndSet(DatabaseType type, bool createDb)
        {
            try
            {
                DbConnectionFactory.CurrentDatabaseType = type;
                if (createDb)
                {
                    CreateDatabase(type, "LoginDb");
                    CreateDatabase(type, "MainDataDb");
                }

                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    if (type == DatabaseType.SqlServer) InitializeAuthSqlServer(conn);
                    else InitializeAuthPostgres(conn);
                }
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    if (type == DatabaseType.SqlServer) InitializeDataSqlServer(conn);
                    else InitializeDataPostgres(conn);
                }

                PerformMigrations(type);
                MessageBox.Show("Databases initialized successfully!\nDefault credentials: admin / admin", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Initialization failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private static void CreateDatabase(DatabaseType type, string dbName)
        {
            if (type == DatabaseType.SqlServer)
            {
                var builder = new SqlConnectionStringBuilder(Settings.Default.SqlAuthConnString);
                builder.InitialCatalog = "master";
                using (var conn = new SqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    int exists = ExecuteScalar(conn, $"SELECT COUNT(*) FROM sys.databases WHERE name = '{dbName}'");
                    if (exists == 0) ExecuteNonQuery(conn, $"CREATE DATABASE [{dbName}]");
                }
            }
            else
            {
                var builder = new NpgsqlConnectionStringBuilder(Settings.Default.PostgresAuthConnString);
                builder.Database = "postgres";
                using (var conn = new NpgsqlConnection(builder.ConnectionString))
                {
                    conn.Open();
                    int exists = ExecuteScalar(conn, $"SELECT COUNT(*) FROM pg_database WHERE datname = '{dbName.ToLower()}'");
                    if (exists == 0) ExecuteNonQuery(conn, $"CREATE DATABASE \"{dbName}\"");
                }
            }
        }

        private static DbStatus CheckStatus(DatabaseType type, out string errorMsg)
        {
            errorMsg = "OK";
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    string query = (type == DatabaseType.SqlServer)
                        ? "SELECT COUNT(*) FROM sys.tables WHERE name = 'User'"
                        : "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'User'";
                    if (ExecuteScalar(conn, query) == 0) return DbStatus.MissingSchema;
                }
            }
            catch (Exception ex)
            {
                if (IsDbMissingException(ex)) { errorMsg = "LoginDb missing."; return DbStatus.MissingAuthDb; }
                errorMsg = ex.Message; return DbStatus.Unreachable;
            }

            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data)) { conn.Open(); }
            }
            catch (Exception ex)
            {
                if (IsDbMissingException(ex)) { errorMsg = "MainDataDb missing."; return DbStatus.MissingDataDb; }
            }

            return DbStatus.Ready;
        }

        private static bool IsDbMissingException(Exception ex)
        {
            if (ex is SqlException sqlEx && sqlEx.Number == 4060) return true;
            if (ex is PostgresException pgEx && pgEx.SqlState == "3D000") return true;
            return false;
        }

        private static void InitializeAuthSqlServer(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'User'") == 0)
            {
                string createUser = @"CREATE TABLE [User] ([Id] INT IDENTITY(1,1) PRIMARY KEY, [Username] NVARCHAR(50) UNIQUE, [Password] NVARCHAR(100), [Name] NVARCHAR(50), [LastName] NVARCHAR(50), [Email] NVARCHAR(100), [Role] NVARCHAR(50) DEFAULT 'User')";
                ExecuteNonQuery(conn, createUser);

                // FIXED: Use Hash when inserting default admin
                string hash = WPF_LoginForm.Services.PasswordHelper.HashPassword("admin");
                ExecuteNonQuery(conn, $"INSERT INTO [User] (Username, Password, Name, LastName, Email, Role) VALUES ('admin', '{hash}', 'System', 'Admin', 'admin@biosun.com', 'Admin')");
            }
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'Logs'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE [Logs] ([Id] INT IDENTITY(1,1) PRIMARY KEY, [LogLevel] NVARCHAR(50), [Message] NVARCHAR(MAX), [Username] NVARCHAR(50), [Exception] NVARCHAR(MAX), [LogDate] DATETIME DEFAULT GETDATE())");
            }
        }

        private static void InitializeAuthPostgres(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'User'") == 0)
            {
                string createUser = @"CREATE TABLE ""User"" (""Id"" SERIAL PRIMARY KEY, ""Username"" VARCHAR(50) UNIQUE, ""Password"" VARCHAR(100), ""Name"" VARCHAR(50), ""LastName"" VARCHAR(50), ""Email"" VARCHAR(100), ""Role"" VARCHAR(50) DEFAULT 'User')";
                ExecuteNonQuery(conn, createUser);

                // FIXED: Use Hash when inserting default admin
                string hash = WPF_LoginForm.Services.PasswordHelper.HashPassword("admin");
                ExecuteNonQuery(conn, $"INSERT INTO \"User\" (\"Username\", \"Password\", \"Name\", \"LastName\", \"Email\", \"Role\") VALUES ('admin', '{hash}', 'System', 'Admin', 'admin@biosun.com', 'Admin')");
            }
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Logs'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE ""Logs"" (""Id"" SERIAL PRIMARY KEY, ""LogLevel"" VARCHAR(50), ""Message"" TEXT, ""Username"" VARCHAR(50), ""Exception"" TEXT, ""LogDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");
            }
        }

        private static void InitializeDataSqlServer(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'ColumnHierarchyMap'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE [ColumnHierarchyMap] ([Id] INT IDENTITY(1,1) PRIMARY KEY, [OwningDataTableName] NVARCHAR(100), [Part1Value] NVARCHAR(200), [Part2Value] NVARCHAR(200), [Part3Value] NVARCHAR(200), [Part4Value] NVARCHAR(200), [CoreItemDisplayName] NVARCHAR(200), [ActualDataTableColumnName] NVARCHAR(200))");
            }
        }

        private static void InitializeDataPostgres(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ColumnHierarchyMap'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE ""ColumnHierarchyMap"" (""Id"" SERIAL PRIMARY KEY, ""OwningDataTableName"" VARCHAR(100), ""Part1Value"" VARCHAR(200), ""Part2Value"" VARCHAR(200), ""Part3Value"" VARCHAR(200), ""Part4Value"" VARCHAR(200), ""CoreItemDisplayName"" VARCHAR(200), ""ActualDataTableColumnName"" VARCHAR(200))");
            }
        }

        private static int ExecuteScalar(IDbConnection conn, string query)
        {
            using (var cmd = conn.CreateCommand()) { cmd.CommandText = query; var result = cmd.ExecuteScalar(); return result == null ? 0 : Convert.ToInt32(result); }
        }

        private static void ExecuteNonQuery(IDbConnection conn, string query)
        {
            using (var cmd = conn.CreateCommand()) { cmd.CommandText = query; cmd.ExecuteNonQuery(); }
        }
    }
}