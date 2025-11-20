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

        private string Quote(string identifier)
        {
            if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql)
                return $"\"{identifier}\"";
            return $"[{identifier}]";
        }

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
                        if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql)
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

        public async Task<DataTable> GetTableDataAsync(string tableName)
        {
            var dt = new DataTable();
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT * FROM {Quote(tableName)}";
                        FillDataTable(cmd, dt);
                        dt.TableName = tableName;

                        // Identify Primary Key for the DataTable if possible
                        // This helps the CommandBuilder later, though often it needs the DB schema
                        try
                        {
                            var schema = cmd.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SchemaOnly).GetSchemaTable();
                            // We don't manually set PK on DataTable here as it can be complex, 
                            // relying on CommandBuilder's internal logic is usually standard.
                        }
                        catch { /* Ignore schema retrieval errors */ }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching data for {tableName}", ex);
                throw;
            }
            return dt;
        }

        public async Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            var dt = new DataTable();
            try
            {
                var safeColumns = columns.Select(c => Quote(c));
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

        public async Task<List<string>> GetDistinctPart1ValuesAsync(string tableName) => await GetDistinctValuesAsync(tableName, "Part1");
        public async Task<List<string>> GetDistinctPart2ValuesAsync(string tableName, string p1) => await GetDistinctValuesAsync(tableName, "Part2", "Part1", p1);
        public async Task<List<string>> GetDistinctPart3ValuesAsync(string tableName, string p1, string p2) => await GetDistinctValuesAsync(tableName, "Part3", new Dictionary<string, string> { { "Part1", p1 }, { "Part2", p2 } });
        public async Task<List<string>> GetDistinctPart4ValuesAsync(string tableName, string p1, string p2, string p3) => await GetDistinctValuesAsync(tableName, "Part4", new Dictionary<string, string> { { "Part1", p1 }, { "Part2", p2 }, { "Part3", p3 } });
        public async Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string tableName, string p1, string p2, string p3, string p4) => await GetDistinctValuesAsync(tableName, "CoreItem", new Dictionary<string, string> { { "Part1", p1 }, { "Part2", p2 }, { "Part3", p3 }, { "Part4", p4 } });
        public async Task<string> GetActualColumnNameAsync(string tableName, string p1, string p2, string p3, string p4, string coreItem) { await Task.CompletedTask; return coreItem; }

        private async Task<List<string>> GetDistinctValuesAsync(string tableName, string targetCol, string filterCol = null, string filterVal = null)
        {
            var filters = new Dictionary<string, string>();
            if (filterCol != null) filters.Add(filterCol, filterVal);
            return await GetDistinctValuesAsync(tableName, targetCol, filters);
        }

        private async Task<List<string>> GetDistinctValuesAsync(string tableName, string targetCol, Dictionary<string, string> filters)
        {
            var list = new List<string>();
            try
            {
                if (!await ColumnExists(tableName, targetCol)) return list;
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        StringBuilder sb = new StringBuilder();
                        sb.Append($"SELECT DISTINCT {Quote(targetCol)} FROM {Quote(tableName)} WHERE 1=1");
                        if (filters != null)
                        {
                            foreach (var kvp in filters)
                            {
                                if (!string.IsNullOrEmpty(kvp.Value))
                                {
                                    sb.Append($" AND {Quote(kvp.Key)} = @{kvp.Key}");
                                    AddParameter(cmd, $"@{kvp.Key}", kvp.Value);
                                }
                            }
                        }
                        sb.Append($" ORDER BY {Quote(targetCol)}");
                        cmd.CommandText = sb.ToString();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read()) if (reader[0] != DBNull.Value) list.Add(reader[0].ToString());
                        }
                    }
                }
            }
            catch (Exception ex) { _logger.LogError($"Error distinct {targetCol}", ex); }
            return list;
        }

        private async Task<bool> ColumnExists(string tableName, string columnName)
        {
            try
            {
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT * FROM {Quote(tableName)} WHERE 1=0";
                        using (var reader = cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                        {
                            var table = reader.GetSchemaTable();
                            foreach (DataRow row in table.Rows) if (row["ColumnName"].ToString().Equals(columnName, StringComparison.OrdinalIgnoreCase)) return true;
                        }
                    }
                }
            }
            catch { return false; }
            return false;
        }

        // --- SAVE CHANGES (Fixes Crash) ---
        public async Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName)
        {
            try
            {
                if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer)
                {
                    using (var conn = new SqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        var adapter = new SqlDataAdapter($"SELECT * FROM {Quote(tableName)} WHERE 1=0", conn);
                        var builder = new SqlCommandBuilder(adapter);

                        // Explicitly checks if the builder can determine key info
                        // This prevents the generic 'crash' and returns a clean error
                        try
                        {
                            adapter.Update(changes);
                        }
                        catch (InvalidOperationException invEx) when (invEx.Message.Contains("Dynamic SQL generation"))
                        {
                            return (false, $"Table '{tableName}' has no Primary Key defined. Editing is not supported.");
                        }

                        return (true, null);
                    }
                }
                else
                {
                    using (var conn = new NpgsqlConnection(DbConnectionFactory.GetConnection(ConnectionTarget.Data).ConnectionString))
                    {
                        var adapter = new NpgsqlDataAdapter($"SELECT * FROM {Quote(tableName)} WHERE 1=0", conn);
                        var builder = new NpgsqlCommandBuilder(adapter);
                        builder.QuotePrefix = "\"";
                        builder.QuoteSuffix = "\"";

                        try
                        {
                            adapter.Update(changes);
                        }
                        catch (InvalidOperationException invEx) when (invEx.Message.Contains("Dynamic SQL generation"))
                        {
                            return (false, $"Table '{tableName}' has no Primary Key defined. Editing is not supported.");
                        }

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
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"DROP TABLE {Quote(tableName)}";
                        await Task.Run(() => cmd.ExecuteNonQuery());
                        return true;
                    }
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
                    string type = col.SelectedDataType;
                    if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql)
                    {
                        if (type.Contains("IDENTITY")) type = "SERIAL";
                        else if (type.Contains("datetime")) type = "TIMESTAMP";
                        else if (type.Contains("nvarchar")) type = "VARCHAR";
                    }
                    string line = $"{Quote(col.DestinationColumnName)} {type}";
                    if (col.IsPrimaryKey) line += " PRIMARY KEY";
                    defs.Add(line);
                }
                sb.Append(string.Join(", ", defs));
                sb.Append(")");
                using (var conn = DbConnectionFactory.GetConnection(ConnectionTarget.Data))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sb.ToString();
                        cmd.ExecuteNonQuery();
                    }
                }
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data)
        {
            try
            {
                if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.SqlServer)
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

        private void FillDataTable(IDbCommand cmd, DataTable dt)
        {
            if (DbConnectionFactory.CurrentDatabaseType == DatabaseType.PostgreSql)
                using (var adapter = new NpgsqlDataAdapter((NpgsqlCommand)cmd)) adapter.Fill(dt);
            else
                using (var adapter = new SqlDataAdapter((SqlCommand)cmd)) adapter.Fill(dt);
        }

        private void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}