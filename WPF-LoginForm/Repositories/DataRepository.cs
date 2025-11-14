// In WPF_LoginForm.Repositories/DataRepository.cs
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using WPF_LoginForm.Services;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public class DataRepository : IDataRepository
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;

        public DataRepository(ILogger logger = null)
        {
            _logger = logger ?? new FileLogger();
            _connectionString = ConfigurationManager.ConnectionStrings["TestDTConnection"]?.ConnectionString;
            if (string.IsNullOrEmpty(_connectionString))
            {
                _logger.LogError("Connection string 'TestDTConnection' not found or empty in App.config.");
                throw new ConfigurationErrorsException("Connection string 'TestDTConnection' not found or empty in App.config.");
            }
            _logger.LogInfo("DataRepository initialized.");
        }

        // --- THIS METHOD IS THE KEY CHANGE ---
        public async Task<DataTable> GetDataAsync(string tableName, List<string> columnsToSelect, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            var dataTable = new DataTable();
            if (string.IsNullOrEmpty(tableName) || columnsToSelect == null || !columnsToSelect.Any())
            {
                return dataTable;
            }
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string safeColumnList = string.Join(", ", columnsToSelect.Select(SanitizeObjectName));

                    var sb = new StringBuilder();
                    sb.Append($"SELECT {safeColumnList} FROM {SanitizeObjectName(tableName)}");

                    // Dynamically add WHERE clause only if dates are provided
                    if (startDate.HasValue && endDate.HasValue && !string.IsNullOrEmpty(dateColumn))
                    {
                        sb.Append($" WHERE {SanitizeObjectName(dateColumn)} BETWEEN @startDate AND @endDate");
                    }

                    using (var command = new SqlCommand(sb.ToString(), connection))
                    {
                        if (startDate.HasValue && endDate.HasValue)
                        {
                            command.Parameters.AddWithValue("@startDate", startDate.Value);
                            command.Parameters.AddWithValue("@endDate", endDate.Value);
                        }

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            await Task.Run(() => adapter.Fill(dataTable));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch data for table '{tableName}' with date range.", ex);
            }
            return dataTable;
        }

        #region Unchanged Methods
        public async Task<List<string>> GetTableNamesAsync()
        {
            _logger.LogInfo("Fetching table names...");
            var fetchedTableNames = new List<string>();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    string query = "SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_SCHEMA, TABLE_NAME";
                    using (var command = new SqlCommand(query, connection))
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            fetchedTableNames.Add(reader.GetString(1));
                        }
                    }
                }
                _logger.LogInfo($"Fetched {fetchedTableNames.Count} table names.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to fetch table names.", ex);
                throw;
            }
            return fetchedTableNames;
        }

        public async Task<DataTable> GetTableDataAsync(string tableName)
        {
            _logger.LogInfo($"Fetching data and schema for table '{tableName}'...");
            if (string.IsNullOrEmpty(tableName))
            {
                _logger.LogError("GetTableDataAsync called with null or empty table name.");
                throw new ArgumentNullException(nameof(tableName));
            }

            var dataTable = new DataTable(tableName);
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    string safeTableName = SanitizeObjectName(tableName);
                    string query = $"SELECT * FROM {safeTableName}";

                    using (var adapter = new SqlDataAdapter(query, connection))
                    {
                        adapter.FillSchema(dataTable, SchemaType.Source);
                        await Task.Run(() => adapter.Fill(dataTable));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to fetch data/schema for table '{tableName}'.", ex);
                throw;
            }
            return dataTable;
        }

        public async Task<(DateTime MinDate, DateTime MaxDate)> GetDateRangeAsync(string tableName, string dateColumnName)
        {
            if (string.IsNullOrEmpty(tableName) || string.IsNullOrEmpty(dateColumnName))
            {
                return (DateTime.MinValue, DateTime.MinValue);
            }
            DateTime minDate = DateTime.MinValue;
            DateTime maxDate = DateTime.MinValue;
            string query = $"SELECT MIN({SanitizeObjectName(dateColumnName)}), MAX({SanitizeObjectName(dateColumnName)}) FROM {SanitizeObjectName(tableName)}";
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    await connection.OpenAsync();
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            if (!await reader.IsDBNullAsync(0)) minDate = reader.GetDateTime(0);
                            if (!await reader.IsDBNullAsync(1)) maxDate = reader.GetDateTime(1);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to get date range for {tableName}.{dateColumnName}", ex);
            }
            return (minDate, maxDate);
        }

        public async Task<bool> DeleteTableAsync(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return false;

            string safeTableName = SanitizeObjectName(tableName);
            string query = $"DROP TABLE {safeTableName}";

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
                _logger.LogInfo($"Successfully deleted table '{tableName}'.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to delete table '{tableName}'.", ex);
                return false;
            }
        }

        public async Task<bool> TableExistsAsync(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return false;

            string query = "SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@TableName", tableName);
                    await connection.OpenAsync();
                    var result = await command.ExecuteScalarAsync();
                    return result != null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking if table '{tableName}' exists.", ex);
                return false;
            }
        }

        public async Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema)
        {
            if (string.IsNullOrEmpty(tableName) || schema == null || !schema.Any())
            {
                return (false, "Table name or schema cannot be empty.");
            }

            var pk = schema.FirstOrDefault(s => s.IsPrimaryKey);
            if (pk == null)
            {
                return (false, "A primary key must be selected.");
            }

            var sb = new StringBuilder();
            sb.AppendLine($"CREATE TABLE {SanitizeObjectName(tableName)} (");

            foreach (var col in schema)
            {
                string dataType;
                switch (col.SelectedDataType)
                {
                    case "Number (int) IDENTITY(1,1)": dataType = "INT IDENTITY(1,1)"; break; // Special case for auto ID
                    case "Number (int)": dataType = "INT"; break;
                    case "Decimal (decimal)": dataType = "DECIMAL(18, 5)"; break;
                    case "Date (datetime)": dataType = "DATETIME"; break;
                    case "Text (string)":
                    default: dataType = "NVARCHAR(MAX)"; break;
                }

                string nullability = col.IsPrimaryKey ? "NOT NULL" : "NULL";
                sb.AppendLine($"  {SanitizeObjectName(col.DestinationColumnName)} {dataType} {nullability},");
            }

            string constraintName = SanitizeObjectName($"PK_{tableName.Replace(" ", "_")}");
            sb.AppendLine($"  CONSTRAINT {constraintName} PRIMARY KEY CLUSTERED ({SanitizeObjectName(pk.DestinationColumnName)} ASC)");
            sb.AppendLine(");");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(sb.ToString(), connection))
                {
                    await connection.OpenAsync();
                    await command.ExecuteNonQueryAsync();
                }
                _logger.LogInfo($"Successfully created table '{tableName}'.");
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create table '{tableName}'. SQL: {sb.ToString()}", ex);
                return (false, $"Database error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data)
        {
            if (string.IsNullOrEmpty(tableName) || data == null || data.Rows.Count == 0)
            {
                return (true, null);
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                    {
                        bulkCopy.DestinationTableName = tableName;

                        try
                        {
                            foreach (DataColumn col in data.Columns)
                            {
                                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                            }

                            await bulkCopy.WriteToServerAsync(data);
                            transaction.Commit();
                            _logger.LogInfo($"Successfully bulk imported {data.Rows.Count} rows into '{tableName}'.");
                            return (true, null);
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            _logger.LogError($"Failed to bulk import data into '{tableName}'.", ex);
                            return (false, $"Bulk import error: {ex.Message}");
                        }
                    }
                }
            }
        }

        private string SanitizeObjectName(string objectName) { if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name cannot be empty."); if (objectName.StartsWith("[") && objectName.EndsWith("]")) return objectName; return $"[{objectName.Replace("]", "]]")}]"; }
        private string SanitizeParameterName(string columnName) { var s = Regex.Replace(columnName, @"[\s\(\)\-\.\/\\#]", "_"); if (s.Length > 0 && char.IsDigit(s[0])) s = "_" + s; s = Regex.Replace(s, @"_+", "_"); if (s.Length > 127) s = s.Substring(0, 127); return "@" + s; }
        public async Task<bool> SaveChangesAsync(DataTable changes, string tableName) { if (changes == null || changes.Rows.Count == 0) return true; if (string.IsNullOrEmpty(tableName)) return false; string safeTableName = SanitizeObjectName(tableName); using (var connection = new SqlConnection(_connectionString)) { await connection.OpenAsync(); using (var transaction = connection.BeginTransaction()) { try { foreach (DataRow row in changes.Rows) { switch (row.RowState) { case DataRowState.Added: await ExecuteInsertAsync(row, safeTableName, connection, transaction); break; case DataRowState.Modified: await ExecuteUpdateAsync(row, safeTableName, connection, transaction); break; case DataRowState.Deleted: await ExecuteDeleteAsync(row, safeTableName, connection, transaction); break; } } transaction.Commit(); return true; } catch (Exception ex) { _logger.LogError($"[SaveChangesAsync] Error for table '{safeTableName}'. Rolling back.", ex); transaction.Rollback(); return false; } } } }
        private async Task ExecuteInsertAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction) { var c = new StringBuilder(); var v = new StringBuilder(); var p = new List<SqlParameter>(); foreach (DataColumn col in row.Table.Columns) { if (col.AutoIncrement) continue; string pName = SanitizeParameterName(col.ColumnName); if (c.Length > 0) { c.Append(", "); v.Append(", "); } c.Append(SanitizeObjectName(col.ColumnName)); v.Append(pName); p.Add(new SqlParameter(pName, row[col] ?? DBNull.Value)); } string sql = $"INSERT INTO {safeTableName} ({c}) VALUES ({v})"; if (c.Length == 0) sql = $"INSERT INTO {safeTableName} DEFAULT VALUES"; using (var cmd = new SqlCommand(sql, connection, transaction)) { if (p.Any()) cmd.Parameters.AddRange(p.ToArray()); await cmd.ExecuteNonQueryAsync(); } }
        private async Task ExecuteUpdateAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction) { var sc = new StringBuilder(); var wc = new StringBuilder(); var p = new List<SqlParameter>(); foreach (DataColumn col in row.Table.Columns) { if (col.AutoIncrement) continue; if (!Equals(row[col, DataRowVersion.Current], row[col, DataRowVersion.Original])) { string pName = SanitizeParameterName("Current_" + col.ColumnName); if (sc.Length > 0) sc.Append(", "); sc.Append($"{SanitizeObjectName(col.ColumnName)} = {pName}"); p.Add(new SqlParameter(pName, row[col, DataRowVersion.Current] ?? DBNull.Value)); } } if (sc.Length == 0) return; DataColumn[] pks = row.Table.PrimaryKey; if (pks != null && pks.Length > 0) { foreach (DataColumn pkCol in pks) { string pName = SanitizeParameterName("Original_" + pkCol.ColumnName); if (wc.Length > 0) wc.Append(" AND "); wc.Append($"{SanitizeObjectName(pkCol.ColumnName)} = {pName}"); p.Add(new SqlParameter(pName, row[pkCol, DataRowVersion.Original])); } } else { foreach (DataColumn col in row.Table.Columns) { string pName = SanitizeParameterName("Original_" + col.ColumnName); if (wc.Length > 0) wc.Append(" AND "); object oVal = row[col, DataRowVersion.Original]; if (oVal == DBNull.Value || oVal == null) { wc.Append($"{SanitizeObjectName(col.ColumnName)} IS NULL"); } else { wc.Append($"{SanitizeObjectName(col.ColumnName)} = {pName}"); p.Add(new SqlParameter(pName, oVal)); } } } string sql = $"UPDATE {safeTableName} SET {sc} WHERE {wc}"; using (var cmd = new SqlCommand(sql, connection, transaction)) { cmd.Parameters.AddRange(p.ToArray()); await cmd.ExecuteNonQueryAsync(); } }
        private async Task ExecuteDeleteAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction) { var wc = new StringBuilder(); var p = new List<SqlParameter>(); DataColumn[] pks = row.Table.PrimaryKey; if (pks != null && pks.Length > 0) { foreach (DataColumn pkCol in pks) { string pName = SanitizeParameterName("Original_" + pkCol.ColumnName); if (wc.Length > 0) wc.Append(" AND "); wc.Append($"{SanitizeObjectName(pkCol.ColumnName)} = {pName}"); p.Add(new SqlParameter(pName, row[pkCol, DataRowVersion.Original])); } } else { foreach (DataColumn col in row.Table.Columns) { string pName = SanitizeParameterName("Original_" + col.ColumnName); if (wc.Length > 0) wc.Append(" AND "); object oVal = row[col, DataRowVersion.Original]; if (oVal == DBNull.Value || oVal == null) { wc.Append($"{SanitizeObjectName(col.ColumnName)} IS NULL"); } else { wc.Append($"{SanitizeObjectName(col.ColumnName)} = {pName}"); p.Add(new SqlParameter(pName, oVal)); } } } string sql = $"DELETE FROM {safeTableName} WHERE {wc}"; using (var cmd = new SqlCommand(sql, connection, transaction)) { cmd.Parameters.AddRange(p.ToArray()); await cmd.ExecuteNonQueryAsync(); } }
        private async Task<List<string>> GetDistinctPartValuesHelperAsync(string owningTableName, string selectColumnName, string p1, string p2, string p3, string p4, string orderBy) { var v = new List<string>(); if (string.IsNullOrEmpty(owningTableName) || string.IsNullOrEmpty(selectColumnName)) return v; string effOrderBy = orderBy ?? selectColumnName; var sb = new StringBuilder($"SELECT {SanitizeObjectName(selectColumnName)} FROM ColumnHierarchyMap WHERE OwningDataTableName = @OwningTableName AND IsEnabled = 1 "); var p = new List<SqlParameter> { new SqlParameter("@OwningTableName", owningTableName) }; Action<string, string> addCond = (col, val) => { if (val != null) { sb.Append($"AND {SanitizeObjectName(col)} = @{col}Param "); p.Add(new SqlParameter($"@{col}Param", val)); } else { sb.Append($"AND {SanitizeObjectName(col)} IS NULL "); } }; if (!selectColumnName.Equals("Part1Value", StringComparison.OrdinalIgnoreCase)) { if (string.IsNullOrEmpty(p1)) return v; addCond("Part1Value", p1); } if (!selectColumnName.StartsWith("Part2", StringComparison.OrdinalIgnoreCase) && !selectColumnName.StartsWith("Part1", StringComparison.OrdinalIgnoreCase)) addCond("Part2Value", p2); if (!selectColumnName.StartsWith("Part3", StringComparison.OrdinalIgnoreCase) && !selectColumnName.StartsWith("Part2", StringComparison.OrdinalIgnoreCase) && !selectColumnName.StartsWith("Part1", StringComparison.OrdinalIgnoreCase)) addCond("Part3Value", p3); if (selectColumnName.Equals("CoreItemDisplayName", StringComparison.OrdinalIgnoreCase)) addCond("Part4Value", p4); if (selectColumnName.StartsWith("Part", StringComparison.OrdinalIgnoreCase)) sb.Append($"AND {SanitizeObjectName(selectColumnName)} IS NOT NULL "); sb.Append($"GROUP BY {SanitizeObjectName(selectColumnName)} ORDER BY MIN({SanitizeObjectName(effOrderBy)}), {SanitizeObjectName(selectColumnName)}"); using (var conn = new SqlConnection(_connectionString)) using (var cmd = new SqlCommand(sb.ToString(), conn)) { await conn.OpenAsync(); cmd.Parameters.AddRange(p.ToArray()); using (var r = await cmd.ExecuteReaderAsync()) { while (await r.ReadAsync()) { if (!r.IsDBNull(0)) v.Add(r.GetString(0)); } } } return v; }
        public Task<List<string>> GetDistinctPart1ValuesAsync(string owningTableName) => GetDistinctPartValuesHelperAsync(owningTableName, "Part1Value", null, null, null, null, "Part1DisplayOrder");
        public Task<List<string>> GetDistinctPart2ValuesAsync(string owningTableName, string part1Value) => GetDistinctPartValuesHelperAsync(owningTableName, "Part2Value", part1Value, null, null, null, "Part2DisplayOrder");
        public Task<List<string>> GetDistinctPart3ValuesAsync(string owningTableName, string part1Value, string part2Value) => GetDistinctPartValuesHelperAsync(owningTableName, "Part3Value", part1Value, part2Value, null, null, "Part3DisplayOrder");
        public Task<List<string>> GetDistinctPart4ValuesAsync(string owningTableName, string part1Value, string part2Value, string part3Value) => GetDistinctPartValuesHelperAsync(owningTableName, "Part4Value", part1Value, part2Value, part3Value, null, "Part4DisplayOrder");
        public Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string owningTableName, string part1Value, string part2Value, string part3Value, string part4Value) => GetDistinctPartValuesHelperAsync(owningTableName, "CoreItemDisplayName", part1Value, part2Value, part3Value, part4Value, "CoreItemDisplayOrder");
        public async Task<string> GetActualColumnNameAsync(string owningTableName, string p1, string p2, string p3, string p4, string core) { if (string.IsNullOrEmpty(owningTableName) || string.IsNullOrEmpty(p1) || string.IsNullOrEmpty(core)) return null; var sb = new StringBuilder("SELECT ActualDataTableColumnName FROM ColumnHierarchyMap WHERE OwningDataTableName = @OwningTableName AND Part1Value = @Part1Value AND CoreItemDisplayName = @CoreItemDisplayName AND IsEnabled = 1 "); var p = new List<SqlParameter> { new SqlParameter("@OwningTableName", owningTableName), new SqlParameter("@Part1Value", p1), new SqlParameter("@CoreItemDisplayName", core) }; Action<string, string> addCond = (col, val) => { if (val != null) { sb.Append($"AND {SanitizeObjectName(col)} = @{col}Param "); p.Add(new SqlParameter($"@{col}Param", val)); } else { sb.Append($"AND {SanitizeObjectName(col)} IS NULL "); } }; addCond("Part2Value", p2); addCond("Part3Value", p3); addCond("Part4Value", p4); using (var conn = new SqlConnection(_connectionString)) using (var cmd = new SqlCommand(sb.ToString(), conn)) { await conn.OpenAsync(); cmd.Parameters.AddRange(p.ToArray()); object res = await cmd.ExecuteScalarAsync(); return res?.ToString(); } }
        #endregion
    }
}