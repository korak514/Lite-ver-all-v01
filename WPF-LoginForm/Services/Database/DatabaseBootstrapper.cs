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
        { Unreachable, MissingDb, MissingSchema, Ready }

        private static string _sqlErrorDetails = "";
        private static string _pgErrorDetails = "";

        public static bool Run()
        {
            // 1. Check Connection Status
            var sqlStatus = CheckStatus(DatabaseType.SqlServer, out _sqlErrorDetails);
            var pgStatus = CheckStatus(DatabaseType.PostgreSql, out _pgErrorDetails);

            // 2. If already ready, run migrations to ensure latest schema updates (e.g. Role column)
            if (sqlStatus == DbStatus.Ready && pgStatus == DbStatus.Ready)
            {
                PerformMigrations(DbConnectionFactory.CurrentDatabaseType);
                return true;
            }

            // 3. Handle Missing Database scenarios
            if (sqlStatus == DbStatus.MissingDb || sqlStatus == DbStatus.MissingSchema)
            {
                if (AskToInitialize("SQL Server", sqlStatus == DbStatus.MissingDb))
                    return InitializeAndSet(DatabaseType.SqlServer, sqlStatus == DbStatus.MissingDb);
                return false;
            }

            if (pgStatus == DbStatus.MissingDb || pgStatus == DbStatus.MissingSchema)
            {
                if (AskToInitialize("PostgreSQL", pgStatus == DbStatus.MissingDb))
                    return InitializeAndSet(DatabaseType.PostgreSql, pgStatus == DbStatus.MissingDb);
                return false;
            }

            // 4. Fallback if one provider is ready but not the current default
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

            string msg = $"No valid database connection found.\n\nSQL Error: {_sqlErrorDetails}\nPG Error: {_pgErrorDetails}\n\nPlease check Settings.";
            MessageBox.Show(msg, "Critical Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }

        private static void PerformMigrations(DatabaseType type)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    if (type == DatabaseType.SqlServer)
                    {
                        // SQL Server Migration: Check for 'Role' column
                        string checkColQuery = "SELECT 1 FROM sys.columns WHERE Name = N'Role' AND Object_ID = Object_ID(N'[User]')";
                        int colExists = ExecuteScalar(conn, checkColQuery);

                        if (colExists == 0)
                        {
                            string addColQuery = "ALTER TABLE [User] ADD [Role] NVARCHAR(50) DEFAULT 'User' WITH VALUES";
                            ExecuteNonQuery(conn, addColQuery);
                        }

                        // Ensure Admin privileges
                        ExecuteNonQuery(conn, "UPDATE [User] SET [Role] = 'Admin' WHERE LOWER([Username]) = 'admin'");
                    }
                    else
                    {
                        // PostgreSQL Migration: Check for 'Role' column using safe DO block
                        string query = @"
                            DO $$
                            BEGIN
                                IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='User' AND column_name='Role') THEN
                                    ALTER TABLE ""User"" ADD COLUMN ""Role"" VARCHAR(50) DEFAULT 'User';
                                END IF;
                            END
                            $$;";
                        ExecuteNonQuery(conn, query);

                        // Ensure Admin privileges
                        ExecuteNonQuery(conn, "UPDATE \"User\" SET \"Role\" = 'Admin' WHERE LOWER(\"Username\") = 'admin'");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration Warning: {ex.Message}");
                // We do not throw here to allow the app to start even if migration fails,
                // though functionality might be limited.
            }
        }

        private static bool AskToInitialize(string providerName, bool needsDbCreation)
        {
            string task = needsDbCreation ? "Create Database & Tables" : "Create Missing Tables";
            var result = MessageBox.Show($"{providerName} connection detected.\nStatus: {task} required.\n\nDo you want to proceed automatically?",
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

                // Run migrations immediately after initialization
                PerformMigrations(type);

                MessageBox.Show("Database initialized successfully!\nDefault credentials: admin / admin", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
                builder.InitialCatalog = "master"; // Connect to master to create DB
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
                builder.Database = "postgres"; // Connect to default postgres DB
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
            string connStr = (type == DatabaseType.SqlServer) ? Settings.Default.SqlAuthConnString : Settings.Default.PostgresAuthConnString;
            try
            {
                using (var conn = (type == DatabaseType.SqlServer) ? (IDbConnection)new SqlConnection(connStr) : new NpgsqlConnection(connStr))
                {
                    conn.Open();
                    // Check if Schema exists by looking for 'User' table
                    string query = (type == DatabaseType.SqlServer)
                        ? "SELECT COUNT(*) FROM sys.tables WHERE name = 'User'"
                        : "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'User'";

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = query;
                        var result = cmd.ExecuteScalar();
                        return (result == null ? 0 : Convert.ToInt32(result)) > 0 ? DbStatus.Ready : DbStatus.MissingSchema;
                    }
                }
            }
            catch (Exception ex)
            {
                bool isMissingDb = false;
                if (ex is SqlException sqlEx && sqlEx.Number == 4060) isMissingDb = true; // Login failed (DB doesn't exist)
                if (ex is PostgresException pgEx && pgEx.SqlState == "3D000") isMissingDb = true; // Invalid Catalog Name

                if (!isMissingDb)
                {
                    // Fallback check: Try connecting to System DB to see if server is up but DB is missing
                    try
                    {
                        string sysDb = (type == DatabaseType.SqlServer) ? "master" : "postgres";
                        if (type == DatabaseType.SqlServer)
                        {
                            var b = new SqlConnectionStringBuilder(connStr); b.InitialCatalog = sysDb;
                            using (var c = new SqlConnection(b.ConnectionString)) { c.Open(); }
                        }
                        else
                        {
                            var b = new NpgsqlConnectionStringBuilder(connStr); b.Database = sysDb;
                            using (var c = new NpgsqlConnection(b.ConnectionString)) { c.Open(); }
                        }
                        // If we connected to system DB, then the specific DB is indeed missing
                        isMissingDb = true;
                    }
                    catch
                    {
                        isMissingDb = false; // Server is genuinely unreachable
                    }
                }

                if (isMissingDb)
                {
                    errorMsg = "Database 'LoginDb' does not exist.";
                    return DbStatus.MissingDb;
                }

                errorMsg = ex.Message;
                return DbStatus.Unreachable;
            }
        }

        private static void InitializeAuthSqlServer(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'User'") == 0)
            {
                string createUser = @"CREATE TABLE [User] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [Username] NVARCHAR(50) NOT NULL UNIQUE,
                    [Password] NVARCHAR(100) NOT NULL,
                    [Name] NVARCHAR(50),
                    [LastName] NVARCHAR(50),
                    [Email] NVARCHAR(100),
                    [Role] NVARCHAR(50) DEFAULT 'User'
                )";
                ExecuteNonQuery(conn, createUser);
                ExecuteNonQuery(conn, "INSERT INTO [User] (Username, Password, Name, LastName, Email, Role) VALUES ('admin', 'admin', 'System', 'Admin', 'admin@biosun.com', 'Admin')");
            }
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'Logs'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE [Logs] ([Id] INT IDENTITY(1,1) PRIMARY KEY, [LogLevel] NVARCHAR(50), [Message] NVARCHAR(MAX), [Username] NVARCHAR(50), [Exception] NVARCHAR(MAX), [LogDate] DATETIME DEFAULT GETDATE())");
            }
        }

        private static void InitializeDataSqlServer(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM sys.tables WHERE name = 'ColumnHierarchyMap'") == 0)
            {
                string createMap = @"CREATE TABLE [ColumnHierarchyMap] (
                    [Id] INT IDENTITY(1,1) PRIMARY KEY,
                    [OwningDataTableName] NVARCHAR(100),
                    [Part1Value] NVARCHAR(200),
                    [Part2Value] NVARCHAR(200),
                    [Part3Value] NVARCHAR(200),
                    [Part4Value] NVARCHAR(200),
                    [CoreItemDisplayName] NVARCHAR(200),
                    [ActualDataTableColumnName] NVARCHAR(200)
                )";
                ExecuteNonQuery(conn, createMap);
            }
        }

        private static void InitializeAuthPostgres(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'User'") == 0)
            {
                string createUser = @"CREATE TABLE ""User"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""Username"" VARCHAR(50) NOT NULL UNIQUE,
                    ""Password"" VARCHAR(100) NOT NULL,
                    ""Name"" VARCHAR(50),
                    ""LastName"" VARCHAR(50),
                    ""Email"" VARCHAR(100),
                    ""Role"" VARCHAR(50) DEFAULT 'User'
                )";
                ExecuteNonQuery(conn, createUser);
                ExecuteNonQuery(conn, "INSERT INTO \"User\" (\"Username\", \"Password\", \"Name\", \"LastName\", \"Email\", \"Role\") VALUES ('admin', 'admin', 'System', 'Admin', 'admin@biosun.com', 'Admin')");
            }
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'Logs'") == 0)
            {
                ExecuteNonQuery(conn, @"CREATE TABLE ""Logs"" (""Id"" SERIAL PRIMARY KEY, ""LogLevel"" VARCHAR(50), ""Message"" TEXT, ""Username"" VARCHAR(50), ""Exception"" TEXT, ""LogDate"" TIMESTAMP DEFAULT CURRENT_TIMESTAMP)");
            }
        }

        private static void InitializeDataPostgres(IDbConnection conn)
        {
            if (ExecuteScalar(conn, "SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'ColumnHierarchyMap'") == 0)
            {
                string createMap = @"CREATE TABLE ""ColumnHierarchyMap"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""OwningDataTableName"" VARCHAR(100),
                    ""Part1Value"" VARCHAR(200),
                    ""Part2Value"" VARCHAR(200),
                    ""Part3Value"" VARCHAR(200),
                    ""Part4Value"" VARCHAR(200),
                    ""CoreItemDisplayName"" VARCHAR(200),
                    ""ActualDataTableColumnName"" VARCHAR(200)
                )";
                ExecuteNonQuery(conn, createMap);
            }
        }

        private static int ExecuteScalar(IDbConnection conn, string query)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                var result = cmd.ExecuteScalar();
                return result == null ? 0 : Convert.ToInt32(result);
            }
        }

        private static void ExecuteNonQuery(IDbConnection conn, string query)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
        }
    }
}