using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using WPF_LoginForm.Models;
using WPF_LoginForm.Properties; // FIX: Resolves 'Settings' error
using WPF_LoginForm.Services;
using WPF_LoginForm.Services.Database;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public class DataRepository : IDataRepository
    {
        private readonly ILogger _logger;
        private readonly CacheService _cache;

        public DataRepository(ILogger logger)
        {
            _logger = logger;
            _cache = new CacheService();
        }

        // Helper property for cleaner code
        private bool IsPostgres => DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql;

        private string Quote(string identifier)
        {
            return IsPostgres ? $"\"{identifier}\"" : $"[{identifier}]";
        }

        private IDbConnection GetConnection() => DbConnectionFactory.GetConnection(ConnectionTarget.Data);

        private IDbConnection GetAuthConnection() => DbConnectionFactory.GetConnection(ConnectionTarget.Auth);

        // =======================================================================
        // 1. DATA RETRIEVAL (Smart Sort & Limit - From Old File)
        // =======================================================================

        public async Task<(DataTable Data, bool IsSortable)> GetTableDataAsync(string tableName, int limit = 0)
        {
            return await DatabaseRetryPolicy.ExecuteAsync(async () =>
            {
                var dt = new DataTable();
                bool isSortable = false;

                try
                {
                    string sortColumn = await GetBestSortColumnAsync(tableName);
                    isSortable = !string.IsNullOrEmpty(sortColumn);

                    using (var conn = GetConnection())
                    {
                        if (conn is SqlConnection sqlConn) await sqlConn.OpenAsync();
                        else if (conn is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                        else conn.Open();

                        using (var cmd = conn.CreateCommand())
                        {
                            string query;
                            string orderByClause = string.Empty;

                            if (isSortable)
                            {
                                orderByClause = $" ORDER BY {Quote(sortColumn)} DESC";
                            }

                            if (IsPostgres)
                            {
                                query = $"SELECT * FROM {Quote(tableName)}{orderByClause}";
                                if (limit > 0) query += $" LIMIT {limit}";
                            }
                            else
                            {
                                if (limit > 0) query = $"SELECT TOP {limit} * FROM {Quote(tableName)}{orderByClause}";
                                else query = $"SELECT * FROM {Quote(tableName)}{orderByClause}";
                            }

                            cmd.CommandText = query;
                            FillDataTable(cmd, dt);
                            dt.TableName = tableName;
                        }
                    }
                    return (dt, isSortable);
                }
                catch (Exception ex)
                {
                    if (IsTransientInternal(ex)) throw;
                    _logger.LogError($"Error fetching data for {tableName}", ex);
                    return (new DataTable(tableName), false);
                }
            });
        }

        private async Task<string> GetBestSortColumnAsync(string tableName)
        {
            try
            {
                var columns = new List<string>();
                using (var conn = GetConnection())
                {
                    if (conn is SqlConnection sqlConn) await sqlConn.OpenAsync();
                    else if (conn is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                    else conn.Open();

                    using (var cmd = conn.CreateCommand())
                    {
                        if (IsPostgres)
                        {
                            cmd.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = @t";
                            AddParameter(cmd, "@t", tableName);
                        }
                        else
                        {
                            cmd.CommandText = $"SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(@t)";
                            AddParameter(cmd, "@t", tableName);
                        }

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) columns.Add(reader[0].ToString());
                        }
                    }
                }

                if (columns.Any(c => c.Equals("ID", StringComparison.OrdinalIgnoreCase))) return "ID";
                if (columns.Any(c => c.Equals("EntryDate", StringComparison.OrdinalIgnoreCase))) return "EntryDate";
                if (columns.Any(c => c.Equals("Date", StringComparison.OrdinalIgnoreCase))) return "Date";
                if (columns.Any(c => c.Equals("Tarih", StringComparison.OrdinalIgnoreCase))) return "Tarih";

                return null;
            }
            catch { return null; }
        }

        // =======================================================================
        // 2. NEW FEATURE: ERROR ANALYTICS
        // =======================================================================

        // ... inside DataRepository class

        public async Task<List<ErrorEventModel>> GetErrorDataAsync(DateTime startDate, DateTime endDate, string tableName)
        {
            var errorList = new List<ErrorEventModel>();
            var dt = new DataTable();

            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        string qTbl = DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer ? $"[{tableName}]" : $"\"{tableName}\"";
                        cmd.CommandText = $"SELECT * FROM {qTbl}";
                        using (var reader = cmd.ExecuteReader()) dt.Load(reader);
                    }
                }

                await Task.Run(() =>
                {
                    DataColumn dateCol = null, shiftCol = null, stopTimeCol = null;

                    // 1. Find Date
                    foreach (DataColumn c in dt.Columns)
                    {
                        var n = c.ColumnName.ToLower();
                        if (n.Contains("date") || n.Contains("tarih")) { dateCol = c; break; }
                    }

                    // 2. Find Shift
                    foreach (DataColumn c in dt.Columns)
                    {
                        var n = c.ColumnName.ToLower();
                        if (n.Contains("shift") || n.Contains("vardiye") || n.Contains("vardiya")) { shiftCol = c; break; }
                    }

                    // 3. Find Stop Time (Duraklama Süresi) - ROBUST MATCHING
                    foreach (DataColumn c in dt.Columns)
                    {
                        var n = c.ColumnName.ToLower();
                        // Matches "Duraklama Süresi", "DuraklamaSuresi", "Durus", etc.
                        if (n.Contains("duraklama") || n.Contains("stop") || n.Contains("durus"))
                        {
                            stopTimeCol = c;
                            break;
                        }
                    }

                    // 4. FALLBACK: Use Column Index 4 (5th column) if name match fails
                    // Based on your screenshot: Date(0), Shift(1), Start(2), End(3), Duration(4)
                    if (stopTimeCol == null && dt.Columns.Count > 4)
                    {
                        // Check if Col 4 looks like a time/duration (simple check)
                        stopTimeCol = dt.Columns[4];
                        _logger.LogInfo($"Warning: 'Duraklama Süresi' column not found by name. Using column index 4: '{stopTimeCol.ColumnName}'");
                    }

                    if (dateCol == null) return;

                    foreach (DataRow row in dt.Rows)
                    {
                        if (row[dateCol] == DBNull.Value) continue;
                        DateTime date = Convert.ToDateTime(row[dateCol]);
                        if (date < startDate || date > endDate) continue;

                        string shift = shiftCol != null ? row[shiftCol].ToString() : "Unknown";

                        // --- TIME PARSING ---
                        int rowTotalMinutes = 0;
                        if (stopTimeCol != null && row[stopTimeCol] != DBNull.Value)
                        {
                            var val = row[stopTimeCol];
                            if (val is DateTime dVal)
                                rowTotalMinutes = (int)dVal.TimeOfDay.TotalMinutes;
                            else if (val is TimeSpan tsVal)
                                rowTotalMinutes = (int)tsVal.TotalMinutes;
                            else
                            {
                                string sVal = val.ToString();
                                // Handle "04:19" string format
                                if (TimeSpan.TryParse(sVal, out TimeSpan ts))
                                    rowTotalMinutes = (int)ts.TotalMinutes;
                                else if (DateTime.TryParse(sVal, out DateTime dtParsed))
                                    rowTotalMinutes = (int)dtParsed.TimeOfDay.TotalMinutes;
                            }
                        }

                        // Parse Codes
                        foreach (DataColumn col in dt.Columns)
                        {
                            string h = col.ColumnName.ToLower();
                            if (h.StartsWith("code") || h.StartsWith("kod"))
                            {
                                string cellValue = row[col]?.ToString();
                                var errorEvent = ErrorEventModel.Parse(cellValue, date, shift, rowTotalMinutes);
                                if (errorEvent != null) errorList.Add(errorEvent);
                            }
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching analytics data", ex);
            }
            return errorList;
        }

        // =======================================================================
        // 3. NEW FEATURE: RENAME COLUMN
        // =======================================================================

        public async Task<(bool Success, string ErrorMessage)> RenameColumnAsync(string tableName, string oldName, string newName)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        if (!IsPostgres)
                        {
                            // SQL Server
                            command.CommandText = "sp_rename";
                            command.CommandType = CommandType.StoredProcedure;
                            AddParameter(command, "@objname", $"{tableName}.{oldName}");
                            AddParameter(command, "@newname", newName);
                            AddParameter(command, "@objtype", "COLUMN");
                            await Task.Run(() => command.ExecuteNonQuery());
                        }
                        else
                        {
                            // PostgreSQL
                            command.CommandText = $"ALTER TABLE \"{tableName}\" RENAME COLUMN \"{oldName}\" TO \"{newName}\"";
                            if (command is NpgsqlCommand pgCmd) await pgCmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                _cache.Clear(); // Clear cache as schema changed
                return (true, string.Empty);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // =======================================================================
        // 4. DATA MANIPULATION (Create, Import, Save, Delete)
        // =======================================================================

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
                    string pk = col.IsPrimaryKey ? "PRIMARY KEY" : "";
                    if (col.SourceColumnName.Contains("Auto-Generated")) type = IsPostgres ? "SERIAL" : "INT IDENTITY(1,1)";
                    defs.Add($"{Quote(col.DestinationColumnName)} {type} {pk}");
                }
                sb.Append(string.Join(", ", defs));
                sb.Append(")");

                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand()) { cmd.CommandText = sb.ToString(); cmd.ExecuteNonQuery(); }
                }
                _cache.Clear();
                return (true, "");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data)
        {
            try
            {
                if (!IsPostgres)
                {
                    // SQL Server Bulk Copy (Fastest)
                    // Ensure Settings namespace is imported
                    using (var conn = new SqlConnection(Settings.Default.SqlDataConnString))
                    {
                        await conn.OpenAsync();
                        using (var bulk = new SqlBulkCopy(conn))
                        {
                            bulk.DestinationTableName = Quote(tableName);
                            foreach (DataColumn c in data.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
                            await bulk.WriteToServerAsync(data);
                        }
                    }
                }
                else
                {
                    // PostgreSQL Binary Import (Fastest)
                    using (var conn = new NpgsqlConnection(Settings.Default.PostgresDataConnString))
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
                }
                return (true, "");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // Partial change in WPF-LoginForm/Repositories/DataRepository.cs, method SaveChangesAsync

        public async Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName)
        {
            try
            {
                if (changes == null) return (true, "");

                // --- FIX: Detect exact ID column name (ID, id, Id) ---
                string idColName = changes.Columns.Cast<DataColumn>()
                    .FirstOrDefault(c => c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase))?.ColumnName;

                if (string.IsNullOrEmpty(idColName))
                    return (false, "ID column missing in DataTable changes.");
                // -----------------------------------------------------

                using (var conn = GetConnection())
                {
                    conn.Open();
                    foreach (DataRow row in changes.Rows)
                    {
                        if (row.RowState == DataRowState.Deleted)
                        {
                            // FIX: Use parameter for ID (Handles GUIDs/Strings correctly)
                            object idValue = row[idColName, DataRowVersion.Original];

                            string q = DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer
                                ? $"DELETE FROM [{tableName}] WHERE [{idColName}] = @targetId"
                                : $"DELETE FROM \"{tableName}\" WHERE \"{idColName}\" = @targetId";

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = q;
                                AddParameter(cmd, "@targetId", idValue);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (row.RowState == DataRowState.Added)
                        {
                            // FIX: Exclude ID column (Case Insensitive)
                            var cols = changes.Columns.Cast<DataColumn>()
                                .Where(c => !c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase) && !c.ReadOnly).ToList();

                            string colNames = string.Join(",", cols.Select(c => DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer ? $"[{c.ColumnName}]" : $"\"{c.ColumnName}\""));
                            string vals = string.Join(",", cols.Select(c => $"@p{c.Ordinal}"));

                            string q = DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer
                                ? $"INSERT INTO [{tableName}] ({colNames}) VALUES ({vals})"
                                : $"INSERT INTO \"{tableName}\" ({colNames}) VALUES ({vals})";

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = q;
                                foreach (var c in cols) AddParameter(cmd, $"@p{c.Ordinal}", row[c]);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        else if (row.RowState == DataRowState.Modified)
                        {
                            // FIX: Use parameter for ID
                            object idValue = row[idColName];

                            // FIX: Exclude ID column
                            var cols = changes.Columns.Cast<DataColumn>()
                                .Where(c => !c.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase) && !c.ReadOnly).ToList();

                            string sets = string.Join(",", cols.Select(c => (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer ? $"[{c.ColumnName}]" : $"\"{c.ColumnName}\"") + $"=@p{c.Ordinal}"));

                            string q = DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer
                                ? $"UPDATE [{tableName}] SET {sets} WHERE [{idColName}] = @targetId"
                                : $"UPDATE \"{tableName}\" SET {sets} WHERE \"{idColName}\" = @targetId";

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = q;
                                foreach (var c in cols) AddParameter(cmd, $"@p{c.Ordinal}", row[c]);
                                AddParameter(cmd, "@targetId", idValue);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
                return (true, "");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<bool> DeleteTableAsync(string tableName)
        {
            try
            {
                _cache.Clear();
                using (var conn = GetConnection())
                {
                    conn.Open();
                    // Clean metadata first
                    using (var cmdClean = conn.CreateCommand())
                    {
                        cmdClean.CommandText = $"DELETE FROM {Quote("ColumnHierarchyMap")} WHERE {Quote("OwningDataTableName")} = @t";
                        AddParameter(cmdClean, "@t", tableName);
                        await Task.Run(() => cmdClean.ExecuteNonQuery());
                    }
                    // Drop table
                    using (var cmdDrop = conn.CreateCommand())
                    {
                        cmdDrop.CommandText = $"DROP TABLE {Quote(tableName)}";
                        await Task.Run(() => cmdDrop.ExecuteNonQuery());
                    }
                }
                return true;
            }
            catch (Exception ex) { _logger.LogError($"Delete failed {tableName}", ex); return false; }
        }

        public async Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName)
        {
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        // Add ID column
                        if (IsPostgres) cmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"ID\" SERIAL";
                        else cmd.CommandText = $"ALTER TABLE [{tableName}] ADD [ID] INT IDENTITY(1,1) NOT NULL";
                        await Task.Run(() => cmd.ExecuteNonQuery());

                        // Make it PK
                        if (IsPostgres) cmd.CommandText = $"ALTER TABLE \"{tableName}\" ADD CONSTRAINT \"PK_{tableName}\" PRIMARY KEY (\"ID\")";
                        else cmd.CommandText = $"ALTER TABLE [{tableName}] ADD CONSTRAINT [PK_{tableName}] PRIMARY KEY ([ID])";
                        await Task.Run(() => cmd.ExecuteNonQuery());
                    }
                }
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // =======================================================================
        // 5. EXISTING HELPERS & LOGS (From Old File)
        // =======================================================================

        public async Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            return await DatabaseRetryPolicy.ExecuteAsync(async () =>
            {
                var dt = new DataTable();
                try
                {
                    var distinctCols = columns.Distinct().ToList();
                    string colString = string.Join(", ", distinctCols.Select(c => Quote(c)));
                    string query = $"SELECT {colString} FROM {Quote(tableName)} WHERE 1=1";

                    if (startDate.HasValue && endDate.HasValue && !string.IsNullOrEmpty(dateColumn))
                    {
                        query += $" AND {Quote(dateColumn)} >= @start AND {Quote(dateColumn)} <= @end ORDER BY {Quote(dateColumn)}";
                    }

                    using (var conn = GetConnection())
                    {
                        if (conn is SqlConnection sqlConn) await sqlConn.OpenAsync();
                        else if (conn is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                        else conn.Open();

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
                    return dt;
                }
                catch (Exception ex)
                {
                    if (IsTransientInternal(ex)) throw;
                    _logger.LogError($"Error filtered data {tableName}", ex);
                    return new DataTable();
                }
            });
        }

        public async Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn)
        {
            string cacheKey = $"DateRange_{tableName}_{dateColumn}";
            var cached = _cache.Get<Tuple<DateTime, DateTime>>(cacheKey);
            if (cached != null) return (cached.Item1, cached.Item2);

            DateTime min = DateTime.Today;
            DateTime max = DateTime.Today;
            try
            {
                using (var conn = GetConnection())
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT MIN({Quote(dateColumn)}), MAX({Quote(dateColumn)}) FROM {Quote(tableName)}";
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && !reader.IsDBNull(0))
                            {
                                min = Convert.ToDateTime(reader[0]);
                                max = Convert.ToDateTime(reader[1]);
                            }
                        }
                    }
                }
                _cache.Set(cacheKey, new Tuple<DateTime, DateTime>(min, max), TimeSpan.FromMinutes(15));
            }
            catch { }
            return (min, max);
        }

        public async Task<List<string>> GetTableNamesAsync()
        {
            var cached = _cache.Get<List<string>>("TableNames");
            if (cached != null && cached.Any()) return cached;

            var list = await DatabaseRetryPolicy.ExecuteAsync(async () =>
            {
                var dbList = new List<string>();
                try
                {
                    using (var conn = GetConnection())
                    {
                        if (conn is SqlConnection sqlConn) await sqlConn.OpenAsync();
                        else if (conn is NpgsqlConnection pgConn) await pgConn.OpenAsync();
                        else conn.Open();

                        using (var cmd = conn.CreateCommand())
                        {
                            if (IsPostgres) cmd.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'";
                            else cmd.CommandText = "SELECT name FROM sys.tables WHERE name != 'sysdiagrams' ORDER BY name";

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read()) dbList.Add(reader[0].ToString());
                            }
                        }
                    }
                }
                catch (Exception ex) { if (IsTransientInternal(ex)) throw; _logger.LogError("Error tables", ex); }
                return dbList;
            });
            if (list.Any()) _cache.Set("TableNames", list, TimeSpan.FromHours(1));
            return list;
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            var tables = await GetTableNamesAsync();
            return tables.Any(t => t.Equals(tableName, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<DataTable> GetSystemLogsAsync()
        {
            var dt = new DataTable();
            try { using (var c = GetAuthConnection()) { c.Open(); using (var cmd = c.CreateCommand()) { cmd.CommandText = $"SELECT * FROM {Quote("Logs")} ORDER BY {Quote("LogDate")} DESC"; FillDataTable(cmd, dt); } } } catch { }
            return dt;
        }

        public async Task<bool> ClearSystemLogsAsync()
        {
            try { using (var c = GetAuthConnection()) { c.Open(); using (var cmd = c.CreateCommand()) { cmd.CommandText = $"DELETE FROM {Quote("Logs")}"; cmd.ExecuteNonQuery(); } } return true; } catch { return false; }
        }

        // --- HIERARCHY HELPERS ---

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
                using (var conn = GetConnection())
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
                        sb.Append($" AND {Quote(targetCol)} IS NOT NULL ORDER BY {Quote(targetCol)}");
                        cmd.CommandText = sb.ToString();
                        using (var reader = cmd.ExecuteReader()) { while (reader.Read()) list.Add(reader[0].ToString()); }
                    }
                }
            }
            catch { }
            return list;
        }

        public async Task<string> GetActualColumnNameAsync(string tableName, string p1, string p2, string p3, string p4, string coreItem)
        {
            // Simplified lookup logic
            // Implementation depends on exact schema of ColumnHierarchyMap, essentially a SELECT query
            // Placeholder for now as this requires exact param mapping
            return null;
        }

        public async Task<bool> ClearHierarchyMapForTableAsync(string tableName)
        {
            try
            {
                using (var conn = GetConnection())
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
            catch { return false; }
        }

        public async Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData)
        {
            return await BulkImportDataAsync("ColumnHierarchyMap", mapData);
        }

        // --- INTERNAL HELPERS ---

        private void FillDataTable(IDbCommand cmd, DataTable dt)
        {
            if (IsPostgres) using (var adapter = new NpgsqlDataAdapter((NpgsqlCommand)cmd)) { adapter.Fill(dt); }
            else using (var adapter = new SqlDataAdapter((SqlCommand)cmd)) { adapter.Fill(dt); }
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private bool IsTransientInternal(Exception ex)
        {
            return ex is SqlException || ex is PostgresException || ex is System.Net.Sockets.SocketException;
        }

        private string GetSqlType(string uiType)
        {
            if (string.IsNullOrEmpty(uiType)) return IsPostgres ? "TEXT" : "NVARCHAR(MAX)";
            uiType = uiType.ToLowerInvariant();
            if (uiType.Contains("identity")) return IsPostgres ? "SERIAL" : "INT IDENTITY(1,1)";
            if (uiType.Contains("int")) return IsPostgres ? "INTEGER" : "INT";
            if (uiType.Contains("decimal")) return IsPostgres ? "DECIMAL" : "DECIMAL(18,2)";
            if (uiType.Contains("date")) return IsPostgres ? "TIMESTAMP" : "DATETIME";
            return IsPostgres ? "TEXT" : "NVARCHAR(MAX)";
        }
    }
}