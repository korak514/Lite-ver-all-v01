using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public class DataRepository : IDataRepository
    {
        private readonly ILogger _logger;

        public DataRepository(ILogger logger)
        {
            _logger = logger;
        }

        private bool IsPostgres => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql;

        private string Quote(string identifier)
        {
            return IsPostgres ? $"\"{identifier}\"" : $"[{identifier}]";
        }

        // --- 1. DATA RETRIEVAL (Optimized) ---
        public async Task<DataTable> GetTableDataAsync(string tableName, int limit = 0)
        {
            var dt = new DataTable();
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        string query;
                        // Try to find a column to sort by (ID or EntryDate) for "Recent" data
                        string orderBy = "ORDER BY 1 DESC"; // Default fallback

                        // NOTE: In a real scenario, you might check schema for specific columns.
                        // For now, we assume ID exists or we rely on default sort.
                        // Ideally: ORDER BY [ID] DESC if ID exists.

                        if (IsPostgres)
                        {
                            query = $"SELECT * FROM {Quote(tableName)}";
                            // Check for ID column presence implicitly or just try sorting
                            // Simple approach: SELECT * FROM table ORDER BY "ID" DESC LIMIT N
                            // We will use a safe generic approach:
                            if (limit > 0) query += $" LIMIT {limit}";
                        }
                        else
                        {
                            // SQL Server: SELECT TOP N * FROM table
                            if (limit > 0)
                                query = $"SELECT TOP {limit} * FROM {Quote(tableName)}";
                            else
                                query = $"SELECT * FROM {Quote(tableName)}";
                        }

                        cmd.CommandText = query;
                        FillDataTable(cmd, dt);
                        dt.TableName = tableName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching data for {tableName}", ex);
                // Return empty table with name so UI doesn't crash
                var emptyDt = new DataTable(tableName);
                return emptyDt;
            }
            return dt;
        }

        public async Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            var dt = new DataTable();
            try
            {
                // Safety: Ensure columns are unique and quoted
                var distinctCols = columns.Distinct().ToList();
                var safeColumns = distinctCols.Select(c => Quote(c));
                string colString = string.Join(", ", safeColumns);

                string query = $"SELECT {colString} FROM {Quote(tableName)} WHERE 1=1";

                if (startDate.HasValue && endDate.HasValue && !string.IsNullOrEmpty(dateColumn))
                {
                    query += $" AND {Quote(dateColumn)} >= @start AND {Quote(dateColumn)} <= @end ORDER BY {Quote(dateColumn)}";
                }

                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = query;
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            AddParameter(cmd, "@start", startDate.Value);
                            AddParameter(cmd, "@end", endDate.Value);
                        }
                        FillDataTable(cmd, dt);
                    }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error filtered data {tableName}", ex); }
            return dt;
        }

        // --- 2. LOGS ---
        public async Task<DataTable> GetSystemLogsAsync()
        {
            var dt = new DataTable();
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        int limit = Properties.Settings.Default.DefaultRowLimit; // Access Global Variable

                        if (IsPostgres)
                            cmd.CommandText = $"SELECT \"LogDate\", \"LogLevel\", \"Username\", \"Message\" FROM \"Logs\" ORDER BY \"LogDate\" DESC LIMIT {limit}";
                        else
                            cmd.CommandText = $"SELECT TOP {limit} [LogDate], [LogLevel], [Username], [Message] FROM [Logs] ORDER BY [LogDate] DESC";
                        FillDataTable(cmd, dt);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading logs: {ex.Message}");
            }
            return dt;
        }

        public async Task<bool> ClearSystemLogsAsync()
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Auth))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        string tableName = IsPostgres ? "\"Logs\"" : "[Logs]";
                        cmd.CommandText = $"TRUNCATE TABLE {tableName}";
                        await Task.Run(() => cmd.ExecuteNonQuery());
                        return true;
                    }
                }
            }
            catch { return false; }
        }

        // --- 3. SCHEMA & METADATA ---
        public async Task<List<string>> GetTableNamesAsync()
        {
            var list = new List<string>();
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        if (IsPostgres)
                            cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
                        else
                            cmd.CommandText = "SELECT name FROM sys.tables WHERE name != 'sysdiagrams' ORDER BY name";

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) list.Add(reader[0].ToString());
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError("Error fetching tables.", ex); }
            return list;
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            var tables = await GetTableNamesAsync();
            return tables.Any(t => t.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<string> GetActualColumnNameAsync(string tableName, string p1, string p2, string p3, string p4, string coreItem)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append($"SELECT {Quote("ActualDataTableColumnName")} FROM {Quote("ColumnHierarchyMap")} WHERE {Quote("OwningDataTableName")} = @t");
                        AddParameter(cmd, "@t", tableName);

                        if (!string.IsNullOrEmpty(p1)) { sb.Append($" AND {Quote("Part1Value")} = @p1"); AddParameter(cmd, "@p1", p1); }
                        if (!string.IsNullOrEmpty(p2)) { sb.Append($" AND {Quote("Part2Value")} = @p2"); AddParameter(cmd, "@p2", p2); }
                        if (!string.IsNullOrEmpty(p3)) { sb.Append($" AND {Quote("Part3Value")} = @p3"); AddParameter(cmd, "@p3", p3); }
                        if (!string.IsNullOrEmpty(coreItem)) { sb.Append($" AND {Quote("CoreItemDisplayName")} = @core"); AddParameter(cmd, "@core", coreItem); }

                        cmd.CommandText = sb.ToString();
                        var result = await Task.Run(() => cmd.ExecuteScalar());
                        return result?.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error resolving target column for {tableName}", ex);
                return null;
            }
        }

        public async Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn)
        {
            DateTime min = DateTime.MinValue;
            DateTime max = DateTime.MinValue;
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT MIN({Quote(dateColumn)}), MAX({Quote(dateColumn)}) FROM {Quote(tableName)}";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                if (reader[0] != DBNull.Value) min = Convert.ToDateTime(reader[0]);
                                if (reader[1] != DBNull.Value) max = Convert.ToDateTime(reader[1]);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error date range {tableName}", ex); }
            return (min, max);
        }

        // --- 4. DISTINCT VALUES & MAP ---
        public async Task<List<string>> GetDistinctPart1ValuesAsync(string tableName) => await GetMapValuesAsync("Part1Value", tableName, null);

        public async Task<List<string>> GetDistinctPart2ValuesAsync(string tableName, string p1) => await GetMapValuesAsync("Part2Value", tableName, new Dictionary<string, string> { { "Part1Value", p1 } });

        public async Task<List<string>> GetDistinctPart3ValuesAsync(string tableName, string p1, string p2) => await GetMapValuesAsync("Part3Value", tableName, new Dictionary<string, string> { { "Part1Value", p1 }, { "Part2Value", p2 } });

        public async Task<List<string>> GetDistinctPart4ValuesAsync(string tableName, string p1, string p2, string p3) => await GetMapValuesAsync("Part4Value", tableName, new Dictionary<string, string> { { "Part1Value", p1 }, { "Part2Value", p2 }, { "Part3Value", p3 } });

        public async Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string tableName, string p1, string p2, string p3, string p4) => await GetMapValuesAsync("CoreItemDisplayName", tableName, new Dictionary<string, string> { { "Part1Value", p1 }, { "Part2Value", p2 }, { "Part3Value", p3 } });

        private async Task<List<string>> GetMapValuesAsync(string targetCol, string tableName, Dictionary<string, string> filters)
        {
            var list = new List<string>();
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append($"SELECT DISTINCT {Quote(targetCol)} FROM {Quote("ColumnHierarchyMap")} WHERE {Quote("OwningDataTableName")} = @t");
                        AddParameter(cmd, "@t", tableName);

                        if (filters != null)
                        {
                            foreach (var kvp in filters)
                            {
                                if (string.IsNullOrEmpty(kvp.Value)) sb.Append($" AND ({Quote(kvp.Key)} IS NULL OR {Quote(kvp.Key)} = '')");
                                else { sb.Append($" AND {Quote(kvp.Key)} = @{kvp.Key}"); AddParameter(cmd, $"@{kvp.Key}", kvp.Value); }
                            }
                        }
                        sb.Append($" AND {Quote(targetCol)} IS NOT NULL AND {Quote(targetCol)} != '' ORDER BY {Quote(targetCol)}");
                        cmd.CommandText = sb.ToString();
                        using (var reader = cmd.ExecuteReader()) { while (reader.Read()) list.Add(reader[0].ToString()); }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error fetching map values for {targetCol}", ex); }
            return list;
        }

        public async Task<bool> ClearHierarchyMapForTableAsync(string tableName)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"DELETE FROM {Quote("ColumnHierarchyMap")} WHERE {Quote("OwningDataTableName")} = @t";
                        AddParameter(cmd, "@t", tableName);
                        await Task.Run(() => cmd.ExecuteNonQuery());
                        return true;
                    }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error clearing hierarchy map for {tableName}", ex); return false; }
        }

        public async Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData) => await BulkImportDataAsync("ColumnHierarchyMap", mapData);

        // --- 5. WRITE OPERATIONS ---
        public async Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName)
        {
            try
            {
                if (changes.PrimaryKey == null || changes.PrimaryKey.Length == 0) return (false, $"The table '{tableName}' does not have a Primary Key (ID).");

                if (!IsPostgres)
                {
                    using (var conn = new SqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        var adapter = new SqlDataAdapter($"SELECT * FROM {Quote(tableName)} WHERE 1=0", conn);
                        var builder = new SqlCommandBuilder(adapter);
                        builder.QuotePrefix = "["; builder.QuoteSuffix = "]";
                        adapter.Update(changes);
                        return (true, null);
                    }
                }
                else
                {
                    using (var conn = new NpgsqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        var adapter = new NpgsqlDataAdapter($"SELECT * FROM {Quote(tableName)} WHERE 1=0", conn);
                        var builder = new NpgsqlCommandBuilder(adapter);
                        builder.QuotePrefix = "\""; builder.QuoteSuffix = "\"";
                        adapter.Update(changes);
                        return (true, null);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving changes to {tableName}", ex);
                return (false, ex.Message);
            }
        }

        public async Task<bool> DeleteTableAsync(string tableName)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = $"DROP TABLE {Quote(tableName)}"; await Task.Run(() => cmd.ExecuteNonQuery()); return true; }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error deleting {tableName}", ex); return false; }
        }

        public async Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"CREATE TABLE {Quote(tableName)} (");
                List<string> defs = new List<string>();
                foreach (var col in schema)
                {
                    string type = GetSqlType(col.SelectedDataType);
                    string line = $"{Quote(col.DestinationColumnName)} {type}";
                    if (col.IsPrimaryKey) line += " PRIMARY KEY";
                    defs.Add(line);
                }
                sb.Append(string.Join(", ", defs));
                sb.Append(")");
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = sb.ToString(); cmd.ExecuteNonQuery(); }
                }
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data)
        {
            try
            {
                if (!IsPostgres)
                {
                    using (var conn = new SqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        conn.Open();
                        using (var bulk = new SqlBulkCopy(conn))
                        {
                            bulk.DestinationTableName = Quote(tableName);
                            foreach (DataColumn col in data.Columns) bulk.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            await bulk.WriteToServerAsync(data);
                        }
                    }
                    return (true, null);
                }
                else
                {
                    using (var conn = new NpgsqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        conn.Open();
                        var columns = data.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName));
                        string copyCommand = $"COPY {Quote(tableName)} ({string.Join(", ", columns)}) FROM STDIN (FORMAT BINARY)";
                        using (var writer = conn.BeginBinaryImport(copyCommand))
                        {
                            foreach (DataRow row in data.Rows)
                            {
                                writer.StartRow();
                                foreach (DataColumn col in data.Columns) writer.Write(row[col]);
                            }
                            writer.Complete();
                        }
                    }
                    return (true, null);
                }
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd1 = conn.CreateCommand())
                    {
                        if (IsPostgres) cmd1.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"ID\" SERIAL";
                        else cmd1.CommandText = $"ALTER TABLE [{tableName}] ADD [ID] INT IDENTITY(1,1) NOT NULL";
                        await Task.Run(() => cmd1.ExecuteNonQuery());
                    }
                    using (var cmd2 = conn.CreateCommand())
                    {
                        if (IsPostgres) cmd2.CommandText = $"ALTER TABLE \"{tableName}\" ADD CONSTRAINT \"PK_{tableName}\" PRIMARY KEY (\"ID\")";
                        else cmd2.CommandText = $"ALTER TABLE [{tableName}] ADD CONSTRAINT [PK_{tableName}] PRIMARY KEY ([ID])";
                        await Task.Run(() => cmd2.ExecuteNonQuery());
                    }
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding ID to {tableName}", ex);
                return (false, ex.Message);
            }
        }

        private void FillDataTable(IDbCommand cmd, DataTable dt)
        {
            if (IsPostgres) using (var adapter = new NpgsqlDataAdapter((NpgsqlCommand)cmd)) { adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey; adapter.Fill(dt); }
            else using (var adapter = new SqlDataAdapter((SqlCommand)cmd)) { adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey; adapter.Fill(dt); }
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private string GetSqlType(string uiType)
        {
            if (string.IsNullOrEmpty(uiType)) return IsPostgres ? "TEXT" : "NVARCHAR(MAX)";
            uiType = uiType.ToLowerInvariant();
            if (uiType.Contains("identity")) return IsPostgres ? "SERIAL" : "INT IDENTITY(1,1)";
            if (uiType.Contains("int") || uiType.Contains("number")) return IsPostgres ? "INTEGER" : "INT";
            if (uiType.Contains("decimal")) return IsPostgres ? "DECIMAL" : "DECIMAL(18,2)";
            if (uiType.Contains("date") || uiType.Contains("time")) return IsPostgres ? "TIMESTAMP" : "DATETIME";
            return IsPostgres ? "TEXT" : "NVARCHAR(MAX)";
        }
    }
}