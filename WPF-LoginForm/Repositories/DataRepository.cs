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

        // --- MODIFIED GetTableDataAsync to include FillSchema ---
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
                    // Connection will be opened by SqlDataAdapter if not already open.
                    // For FillSchema and Fill, it's often managed by the adapter.
                    string safeTableName = SanitizeObjectName(tableName);
                    string query = $"SELECT * FROM {safeTableName}";

                    using (var adapter = new SqlDataAdapter(query, connection))
                    {
                        // 1. Fill Schema first
                        _logger.LogInfo($"[GetTableDataAsync] Filling schema for table '{tableName}'...");
                        // FillSchema configures the DataTable with columns, types, constraints, PKs, and identity info.
                        adapter.FillSchema(dataTable, SchemaType.Source);

                        // 2. Then fill data
                        _logger.LogInfo($"[GetTableDataAsync] Filling data for table '{tableName}'...");
                        int recordCount = await Task.Run(() => adapter.Fill(dataTable));

                        // Debug Log to check AutoIncrement status for "ID" or PK column
                        DataColumn identityColumn = null;
                        if (dataTable.Columns.Contains("ID"))
                        {
                            identityColumn = dataTable.Columns["ID"];
                        }
                        else if (dataTable.PrimaryKey.Length > 0 && dataTable.PrimaryKey[0].AutoIncrement)
                        {
                            identityColumn = dataTable.PrimaryKey[0];
                        }

                        if (identityColumn != null && identityColumn.AutoIncrement)
                        {
                            _logger.LogInfo($"[GetTableDataAsync] For table '{tableName}', Identity Column '{identityColumn.ColumnName}': AutoIncrement='{identityColumn.AutoIncrement}', Seed='{identityColumn.AutoIncrementSeed}', Step='{identityColumn.AutoIncrementStep}', ReadOnly='{identityColumn.ReadOnly}'");
                        }
                        else if (dataTable.Columns.Contains("ID")) // If "ID" exists but not AutoIncrement
                        {
                            _logger.LogWarning($"[GetTableDataAsync] For table '{tableName}', 'ID' Column FOUND but AutoIncrement IS FALSE. AutoIncrement='{dataTable.Columns["ID"].AutoIncrement}', ReadOnly='{dataTable.Columns["ID"].ReadOnly}'");
                        }
                        else
                        {
                            _logger.LogInfo($"[GetTableDataAsync] For table '{tableName}', no 'ID' column found and no auto-incrementing PK identified by default checks.");
                        }

                        _logger.LogInfo($"Fetched {recordCount} rows for table '{tableName}'.");
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

        private string SanitizeObjectName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName)) throw new ArgumentException("Object name cannot be empty.");
            if (objectName.StartsWith("[") && objectName.EndsWith("]")) return objectName;
            return $"[{objectName.Replace("]", "]]")}]";
        }

        private string SanitizeParameterName(string columnName)
        {
            string sanitized = Regex.Replace(columnName, @"[\s\(\)\-\.\/\\#]", "_");
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0])) { sanitized = "_" + sanitized; }
            sanitized = Regex.Replace(sanitized, @"_+", "_");
            if (sanitized.Length > 127) sanitized = sanitized.Substring(0, 127);
            return "@" + sanitized;
        }

        public async Task<bool> SaveChangesAsync(DataTable changes, string tableName)
        {
            if (changes == null || changes.Rows.Count == 0)
            {
                _logger.LogInfo("[SaveChangesAsync] No changes submitted.");
                return true;
            }
            if (string.IsNullOrEmpty(tableName))
            {
                _logger.LogError("[SaveChangesAsync] Error: Table name is null or empty.");
                return false;
            }

            string safeTableName = SanitizeObjectName(tableName);

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                SqlTransaction transaction = null;

                try
                {
                    transaction = connection.BeginTransaction();
                    _logger.LogInfo($"[SaveChangesAsync] Starting transaction for table '{safeTableName}'. Processing {changes.Rows.Count} changed rows.");

                    foreach (DataRow row in changes.Rows)
                    {
                        switch (row.RowState)
                        {
                            case DataRowState.Added:
                                await ExecuteInsertAsync(row, safeTableName, connection, transaction);
                                break;
                            case DataRowState.Modified:
                                await ExecuteUpdateAsync(row, safeTableName, connection, transaction);
                                break;
                            case DataRowState.Deleted:
                                await ExecuteDeleteAsync(row, safeTableName, connection, transaction);
                                break;
                        }
                    }

                    transaction.Commit();
                    _logger.LogInfo($"[SaveChangesAsync] Transaction committed successfully for table '{safeTableName}'.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"[SaveChangesAsync] Error saving changes for table '{safeTableName}'. Rolling back transaction.", ex);
                    try
                    {
                        if (transaction != null && transaction.Connection != null)
                        {
                            transaction.Rollback();
                            _logger.LogInfo("[SaveChangesAsync] Transaction rolled back successfully.");
                        }
                    }
                    catch (Exception rollbackEx)
                    {
                        _logger.LogError("[SaveChangesAsync] CRITICAL: Failed to roll back transaction!", rollbackEx);
                    }
                    return false;
                }
                finally
                {
                    transaction?.Dispose();
                }
            }
        }

        private async Task ExecuteInsertAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction)
        {
            StringBuilder columns = new StringBuilder();
            StringBuilder values = new StringBuilder();
            List<SqlParameter> parameters = new List<SqlParameter>();

            foreach (DataColumn col in row.Table.Columns)
            {
                // Skip AutoIncrement columns (like IDENTITY ID) OR explicitly named "ID" as a fallback
                if (col.AutoIncrement || col.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInfo($"[ExecuteInsertAsync] Skipping column '{col.ColumnName}' (AutoIncrement={col.AutoIncrement}) for INSERT.");
                    continue;
                }

                string paramName = SanitizeParameterName(col.ColumnName);
                if (columns.Length > 0) { columns.Append(", "); values.Append(", "); }
                columns.Append(SanitizeObjectName(col.ColumnName));
                values.Append(paramName);
                object valueToInsert = row[col] == DBNull.Value ? DBNull.Value : row[col];
                parameters.Add(new SqlParameter(paramName, valueToInsert ?? DBNull.Value));
            }

            if (columns.Length == 0)
            {
                // This could happen if a table only has an ID column and another you choose to skip.
                // For a valid INSERT, you might need "INSERT INTO Table DEFAULT VALUES"
                // or ensure there's at least one non-identity column to insert.
                _logger.LogWarning($"[ExecuteInsertAsync] No columns to insert for table {safeTableName} after filtering. If table requires values, this will fail.");
                // To make it insert a row with all default values (if schema allows):
                // string insertSqlDefault = $"INSERT INTO {safeTableName} DEFAULT VALUES";
                // For now, let it proceed; if it's an issue, the SQL Server will error.
                // If you uncomment the default values insert, ensure it's the correct behavior.
                // return; // Or let it try an empty column list if that should form a valid INSERT by other means
            }

            string insertSql = $"INSERT INTO {safeTableName} ({columns}) VALUES ({values})";
            if (columns.Length == 0) // Handle tables that might only have an identity column and others are nullable with defaults
            {
                insertSql = $"INSERT INTO {safeTableName} DEFAULT VALUES";
                _logger.LogInfo($"[ExecuteInsertAsync] No specific columns to insert, using DEFAULT VALUES for table {safeTableName}.");
            }
            else
            {
                _logger.LogInfo($"[ExecuteInsertAsync] SQL: {insertSql}");
            }

            using (var command = new SqlCommand(insertSql, connection, transaction))
            {
                if (parameters.Any()) // Only add parameters if they exist
                {
                    command.Parameters.AddRange(parameters.ToArray());
                }
                await command.ExecuteNonQueryAsync();
            }
        }

        private async Task ExecuteUpdateAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction)
        {
            StringBuilder setClause = new StringBuilder();
            StringBuilder whereClause = new StringBuilder();
            List<SqlParameter> parameters = new List<SqlParameter>();

            foreach (DataColumn col in row.Table.Columns)
            {
                if (col.AutoIncrement) continue; // Cannot update identity columns
                if (!Equals(row[col, DataRowVersion.Current], row[col, DataRowVersion.Original]))
                {
                    string paramNameCurrent = SanitizeParameterName("Current_" + col.ColumnName);
                    if (setClause.Length > 0) setClause.Append(", ");
                    setClause.Append($"{SanitizeObjectName(col.ColumnName)} = {paramNameCurrent}");
                    object currentValue = row[col, DataRowVersion.Current] == DBNull.Value ? DBNull.Value : row[col, DataRowVersion.Current];
                    parameters.Add(new SqlParameter(paramNameCurrent, currentValue ?? DBNull.Value));
                }
            }

            if (setClause.Length == 0) { _logger.LogWarning("[ExecuteUpdateAsync] Row marked Modified but no column changes detected. Skipping."); return; }

            DataColumn[] primaryKeys = row.Table.PrimaryKey;
            bool useAllColsForWhere = true;

            if (primaryKeys != null && primaryKeys.Length > 0)
            {
                useAllColsForWhere = false;
                foreach (DataColumn pkCol in primaryKeys)
                {
                    string paramNameOriginalPK = SanitizeParameterName("Original_" + pkCol.ColumnName);
                    if (whereClause.Length > 0) whereClause.Append(" AND ");
                    whereClause.Append($"{SanitizeObjectName(pkCol.ColumnName)} = {paramNameOriginalPK}");
                    parameters.Add(new SqlParameter(paramNameOriginalPK, row[pkCol, DataRowVersion.Original]));
                }
            }

            if (useAllColsForWhere)
            {
                whereClause.Clear();
                foreach (DataColumn col in row.Table.Columns)
                {
                    string paramNameOriginal = SanitizeParameterName("Original_" + col.ColumnName);
                    if (whereClause.Length > 0) whereClause.Append(" AND ");
                    object originalValue = row[col, DataRowVersion.Original];
                    if (originalValue == DBNull.Value || originalValue == null) { whereClause.Append($"{SanitizeObjectName(col.ColumnName)} IS NULL"); }
                    else { whereClause.Append($"{SanitizeObjectName(col.ColumnName)} = {paramNameOriginal}"); parameters.Add(new SqlParameter(paramNameOriginal, originalValue)); }
                }
            }

            string updateSql = $"UPDATE {safeTableName} SET {setClause} WHERE {whereClause}";
            using (var command = new SqlCommand(updateSql, connection, transaction))
            {
                command.Parameters.AddRange(parameters.ToArray());
                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0) { _logger.LogWarning("[ExecuteUpdateAsync] Update affected 0 rows. Original row might have changed or been deleted."); }
            }
        }

        private async Task ExecuteDeleteAsync(DataRow row, string safeTableName, SqlConnection connection, SqlTransaction transaction)
        {
            StringBuilder whereClause = new StringBuilder();
            List<SqlParameter> parameters = new List<SqlParameter>();

            DataColumn[] primaryKeys = row.Table.PrimaryKey;
            bool useAllColsForWhere = true;

            if (primaryKeys != null && primaryKeys.Length > 0)
            {
                useAllColsForWhere = false;
                foreach (DataColumn pkCol in primaryKeys)
                {
                    string paramNameOriginalPK = SanitizeParameterName("Original_" + pkCol.ColumnName);
                    if (whereClause.Length > 0) whereClause.Append(" AND ");
                    whereClause.Append($"{SanitizeObjectName(pkCol.ColumnName)} = {paramNameOriginalPK}");
                    parameters.Add(new SqlParameter(paramNameOriginalPK, row[pkCol, DataRowVersion.Original]));
                }
            }

            if (useAllColsForWhere)
            {
                whereClause.Clear();
                foreach (DataColumn col in row.Table.Columns)
                {
                    string paramNameOriginal = SanitizeParameterName("Original_" + col.ColumnName);
                    if (whereClause.Length > 0) whereClause.Append(" AND ");
                    object originalValue = row[col, DataRowVersion.Original];
                    if (originalValue == DBNull.Value || originalValue == null) { whereClause.Append($"{SanitizeObjectName(col.ColumnName)} IS NULL"); }
                    else { whereClause.Append($"{SanitizeObjectName(col.ColumnName)} = {paramNameOriginal}"); parameters.Add(new SqlParameter(paramNameOriginal, originalValue)); }
                }
            }

            string deleteSql = $"DELETE FROM {safeTableName} WHERE {whereClause}";
            using (var command = new SqlCommand(deleteSql, connection, transaction))
            {
                command.Parameters.AddRange(parameters.ToArray());
                int rowsAffected = await command.ExecuteNonQueryAsync();
                if (rowsAffected == 0) { _logger.LogWarning("[ExecuteDeleteAsync] Delete affected 0 rows. Row might have already been deleted."); }
            }
        }

        private async Task<List<string>> GetDistinctPartValuesHelperAsync(string owningTableName,
                                                                        string selectColumnName,
                                                                        string part1Value, string part2Value,
                                                                        string part3Value, string part4Value,
                                                                        string orderByColumnNameIfDifferent)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(owningTableName))
            {
                _logger.LogWarning($"[GetDistinctPartValuesHelperAsync] OwningTableName is null or empty when trying to select {selectColumnName}.");
                return values;
            }
            if (string.IsNullOrEmpty(selectColumnName))
            {
                _logger.LogWarning($"[GetDistinctPartValuesHelperAsync] selectColumnName is null or empty for OwningTable {owningTableName}.");
                return values;
            }

            string effectiveOrderByColumn = orderByColumnNameIfDifferent ?? selectColumnName;
            string safeSelectColumnName = SanitizeObjectName(selectColumnName);
            string safeEffectiveOrderByColumn = SanitizeObjectName(effectiveOrderByColumn);

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append($"SELECT {safeSelectColumnName} ");
            sqlBuilder.Append($"FROM ColumnHierarchyMap ");
            sqlBuilder.Append($"WHERE OwningDataTableName = @OwningTableName AND IsEnabled = 1 ");

            var parameters = new List<SqlParameter> { new SqlParameter("@OwningTableName", owningTableName) };

            Action<string, string> AddPartCondition = (columnDbName, partVal) => {
                string safeColumnDbName = SanitizeObjectName(columnDbName);
                string paramName = $"@{columnDbName}FilterParam";
                if (partVal != null)
                {
                    sqlBuilder.Append($"AND {safeColumnDbName} = {paramName} ");
                    parameters.Add(new SqlParameter(paramName, partVal));
                }
                else
                {
                    sqlBuilder.Append($"AND {safeColumnDbName} IS NULL ");
                }
            };

            if (!selectColumnName.Equals("Part1Value", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(part1Value))
                {
                    _logger.LogWarning($"[GetDistinctPartValuesHelperAsync] Attempted to fetch {selectColumnName} without providing Part1Value for table {owningTableName}. Returning empty.");
                    return values;
                }
                AddPartCondition("Part1Value", part1Value);
            }

            if (!selectColumnName.Equals("Part1Value", StringComparison.OrdinalIgnoreCase) &&
                !selectColumnName.Equals("Part2Value", StringComparison.OrdinalIgnoreCase))
            {
                AddPartCondition("Part2Value", part2Value);
            }

            if (!selectColumnName.Equals("Part1Value", StringComparison.OrdinalIgnoreCase) &&
                !selectColumnName.Equals("Part2Value", StringComparison.OrdinalIgnoreCase) &&
                !selectColumnName.Equals("Part3Value", StringComparison.OrdinalIgnoreCase))
            {
                AddPartCondition("Part3Value", part3Value);
            }

            if (selectColumnName.Equals("CoreItemDisplayName", StringComparison.OrdinalIgnoreCase))
            {
                AddPartCondition("Part4Value", part4Value);
            }

            if (selectColumnName.StartsWith("Part", StringComparison.OrdinalIgnoreCase) && selectColumnName.EndsWith("Value", StringComparison.OrdinalIgnoreCase))
            {
                sqlBuilder.Append($"AND {safeSelectColumnName} IS NOT NULL ");
            }

            sqlBuilder.Append($"GROUP BY {safeSelectColumnName} ");
            sqlBuilder.Append($"ORDER BY MIN({safeEffectiveOrderByColumn}), {safeSelectColumnName}");

            _logger.LogInfo($"[GetDistinctPartValuesHelperAsync] SQL for {selectColumnName}: {sqlBuilder.ToString()}");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(sqlBuilder.ToString(), connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    values.Add(reader.GetString(0));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching distinct values for {selectColumnName} from ColumnHierarchyMap. SQL: {sqlBuilder.ToString()}", ex);
            }
            return values;
        }

        public Task<List<string>> GetDistinctPart1ValuesAsync(string owningTableName)
        {
            return GetDistinctPartValuesHelperAsync(owningTableName, "Part1Value",
                null, null, null, null, "Part1DisplayOrder");
        }

        public Task<List<string>> GetDistinctPart2ValuesAsync(string owningTableName, string part1Value)
        {
            if (string.IsNullOrEmpty(part1Value))
            {
                _logger.LogWarning("[GetDistinctPart2ValuesAsync] Part1Value is required.");
                return Task.FromResult(new List<string>());
            }
            return GetDistinctPartValuesHelperAsync(owningTableName, "Part2Value",
                part1Value, null, null, null, "Part2DisplayOrder");
        }

        public Task<List<string>> GetDistinctPart3ValuesAsync(string owningTableName, string part1Value, string part2Value)
        {
            if (string.IsNullOrEmpty(part1Value))
            {
                _logger.LogWarning("[GetDistinctPart3ValuesAsync] Part1Value is required.");
                return Task.FromResult(new List<string>());
            }
            return GetDistinctPartValuesHelperAsync(owningTableName, "Part3Value",
                part1Value, part2Value, null, null, "Part3DisplayOrder");
        }

        public Task<List<string>> GetDistinctPart4ValuesAsync(string owningTableName, string part1Value, string part2Value, string part3Value)
        {
            if (string.IsNullOrEmpty(part1Value))
            {
                _logger.LogWarning("[GetDistinctPart4ValuesAsync] Part1Value is required.");
                return Task.FromResult(new List<string>());
            }
            return GetDistinctPartValuesHelperAsync(owningTableName, "Part4Value",
                part1Value, part2Value, part3Value, null, "Part4DisplayOrder");
        }

        public Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string owningTableName, string part1Value, string part2Value, string part3Value, string part4Value)
        {
            if (string.IsNullOrEmpty(part1Value))
            {
                _logger.LogWarning("[GetDistinctCoreItemDisplayNamesAsync] Part1Value is required.");
                return Task.FromResult(new List<string>());
            }
            return GetDistinctPartValuesHelperAsync(owningTableName, "CoreItemDisplayName",
                part1Value, part2Value, part3Value, part4Value, "CoreItemDisplayOrder");
        }

        public async Task<string> GetActualColumnNameAsync(string owningTableName,
                                                        string part1Value,
                                                        string part2Value,
                                                        string part3Value,
                                                        string part4Value,
                                                        string coreItemDisplayName)
        {
            if (string.IsNullOrEmpty(owningTableName) || string.IsNullOrEmpty(part1Value) || string.IsNullOrEmpty(coreItemDisplayName))
            {
                _logger.LogWarning("[GetActualColumnNameAsync] OwningTable, Part1Value, or CoreItemDisplayName is null/empty.");
                return null;
            }

            var sqlBuilder = new StringBuilder("SELECT ActualDataTableColumnName FROM ColumnHierarchyMap WHERE OwningDataTableName = @OwningTableName AND Part1Value = @Part1Value AND CoreItemDisplayName = @CoreItemDisplayName AND IsEnabled = 1 ");
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@OwningTableName", owningTableName),
                new SqlParameter("@Part1Value", part1Value),
                new SqlParameter("@CoreItemDisplayName", coreItemDisplayName)
            };

            Action<string, string> AddPartCondition = (columnDbName, partVal) => {
                string safeColumnDbName = SanitizeObjectName(columnDbName);
                string paramName = $"@{columnDbName}FilterParam";

                if (partVal != null)
                {
                    sqlBuilder.Append($"AND {safeColumnDbName} = {paramName} ");
                    parameters.Add(new SqlParameter(paramName, partVal));
                }
                else
                {
                    sqlBuilder.Append($"AND {safeColumnDbName} IS NULL ");
                }
            };

            AddPartCondition("Part2Value", part2Value);
            AddPartCondition("Part3Value", part3Value);
            AddPartCondition("Part4Value", part4Value);

            string actualColumn = null;
            _logger.LogInfo($"[GetActualColumnNameAsync] SQL: {sqlBuilder.ToString()}");

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(sqlBuilder.ToString(), connection))
                    {
                        command.Parameters.AddRange(parameters.ToArray());
                        object result = await command.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            actualColumn = result.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching ActualDataTableColumnName. SQL: {sqlBuilder.ToString()}", ex);
            }

            if (actualColumn == null)
            {
                _logger.LogWarning($"[GetActualColumnNameAsync] No matching ActualDataTableColumnName found for P1='{part1Value}', P2='{part2Value}', P3='{part3Value}', P4='{part4Value}', Core='{coreItemDisplayName}' in table '{owningTableName}'");
            }
            return actualColumn;
        }
    }
}