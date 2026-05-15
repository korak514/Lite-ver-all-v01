// Repositories/OfflineDataRepository.cs
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPF_LoginForm.Models;
using WPF_LoginForm.Services;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public class OfflineDataRepository : IDataRepository
    {
        private readonly string _folderPath;
        private readonly ILogger _logger;

        // --- STEP 1: IN-MEMORY CACHE ---
        // Stores the raw CSV data in RAM so we never read the disk twice for the same table.
        private static readonly ConcurrentDictionary<string, DataTable> _tableCache = new ConcurrentDictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

        public OfflineDataRepository(ILogger logger)
        {
            _logger = logger;

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "offline_path.txt");
            if (File.Exists(configPath))
            {
                string savedPath = File.ReadAllText(configPath).Trim();
                if (Directory.Exists(savedPath)) _folderPath = savedPath;
            }

            if (string.IsNullOrEmpty(_folderPath))
            {
                _folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OfflineData");
                if (!Directory.Exists(_folderPath)) Directory.CreateDirectory(_folderPath);
            }
        }

        // --- CORE LOGIC: Get from Cache or Load from Disk ---
        private DataTable GetOrLoadTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return new DataTable();

            if (_tableCache.TryGetValue(tableName, out DataTable cachedTable))
            {
                return cachedTable;
            }

            string filePath = Path.Combine(_folderPath, $"{tableName}.csv");
            DataTable newTable = ReadCsv(filePath);
            newTable.TableName = tableName;

            _tableCache.TryAdd(tableName, newTable);
            return newTable;
        }

        private DataTable ReadCsv(string filePath)
        {
            var dt = new DataTable();
            if (!File.Exists(filePath)) return dt;

            try
            {
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    string headerLine = sr.ReadLine();
                    if (string.IsNullOrWhiteSpace(headerLine)) return dt;

                    // --- NEW: Auto-detect CSV delimiter (Excel in Turkey exports with ';' instead of ',') ---
                    char delimiter = ',';
                    int commaCount = headerLine.Count(c => c == ',');
                    int semiCount = headerLine.Count(c => c == ';');
                    int tabCount = headerLine.Count(c => c == '\t');

                    if (semiCount > commaCount && semiCount > tabCount) delimiter = ';';
                    else if (tabCount > commaCount && tabCount > semiCount) delimiter = '\t';

                    // Pass the detected delimiter to the parser
                    var headers = ParseCsvLine(headerLine, delimiter);
                    foreach (var h in headers)
                    {
                        string colName = h.Trim();
                        if (string.IsNullOrWhiteSpace(colName)) colName = "Column"; // fallback for empty headers

                        int dupCount = 1;
                        string originalColName = colName;
                        while (dt.Columns.Contains(colName))
                        {
                            colName = $"{originalColName}_{dupCount++}";
                        }
                        dt.Columns.Add(colName);
                    }

                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Pass the detected delimiter to the parser
                        var rowValues = ParseCsvLine(line, delimiter);
                        if (rowValues.Count > 0)
                        {
                            var row = dt.NewRow();
                            for (int i = 0; i < Math.Min(rowValues.Count, dt.Columns.Count); i++)
                            {
                                row[i] = rowValues[i];
                            }
                            dt.Rows.Add(row);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error reading CSV {filePath}", ex);
            }

            return dt;
        }

        private List<string> ParseCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            bool inQuotes = false;
            StringBuilder currentValue = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(currentValue.ToString().Trim());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }
            result.Add(currentValue.ToString().Trim());
            return result;
        }

        public async Task<List<string>> GetTableNamesAsync(bool forceRefresh = false)
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(_folderPath)) return new List<string>();
                return Directory.GetFiles(_folderPath, "*.csv")
                                .Select(Path.GetFileNameWithoutExtension)
                                .ToList();
            });
        }

        public async Task<(DataTable Data, bool IsSortable)> GetTableDataAsync(string tableName, int limit = 0)
        {
            return await Task.Run(() =>
            {
                var cachedDt = GetOrLoadTable(tableName);

                // Return a copy so the UI doesn't accidentally modify the cached master data
                if (limit > 0 && cachedDt.Rows.Count > limit)
                {
                    var limitedDt = cachedDt.Clone();
                    for (int i = 0; i < limit; i++) limitedDt.ImportRow(cachedDt.Rows[i]);
                    return (limitedDt, true);
                }

                return (cachedDt.Copy(), true);
            });
        }

        public async Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn)
        {
            return await Task.Run(() =>
            {
                var dt = GetOrLoadTable(tableName);
                DateTime min = DateTime.MaxValue;
                DateTime max = DateTime.MinValue;
                bool found = false;

                if (dt != null && dt.Columns.Contains(dateColumn))
                {
                    int colIdx = dt.Columns.IndexOf(dateColumn);
                    foreach (DataRow row in dt.Rows)
                    {
                        if (row[colIdx] != DBNull.Value && DateTime.TryParse(row[colIdx].ToString(), out DateTime d))
                        {
                            if (d < min) min = d;
                            if (d > max) max = d;
                            found = true;
                        }
                    }
                }

                if (found) return (min, max);
                return (DateTime.Today.AddMonths(-1), DateTime.Today);
            });
        }

        public async Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            return await Task.Run(() =>
            {
                var rawDt = GetOrLoadTable(tableName);
                var finalDt = new DataTable(tableName);

                // 1. Setup Target Columns
                foreach (var col in columns)
                {
                    if (!finalDt.Columns.Contains(col))
                        finalDt.Columns.Add(col, (col == dateColumn) ? typeof(DateTime) : typeof(string));
                }

                if (rawDt.Rows.Count == 0 || finalDt.Columns.Count == 0) return finalDt;

                // 2. Map Columns
                int dateIdx = -1;
                if (!string.IsNullOrEmpty(dateColumn) && rawDt.Columns.Contains(dateColumn))
                    dateIdx = rawDt.Columns.IndexOf(dateColumn);

                var colMappings = new Dictionary<DataColumn, int>();
                foreach (DataColumn targetCol in finalDt.Columns)
                {
                    if (rawDt.Columns.Contains(targetCol.ColumnName))
                        colMappings[targetCol] = rawDt.Columns.IndexOf(targetCol.ColumnName);
                }

                // 3. Filter & Populate In-Memory
                foreach (DataRow rawRow in rawDt.Rows)
                {
                    DateTime rowDate = DateTime.MinValue;
                    if (dateIdx != -1)
                    {
                        string dateStr = rawRow[dateIdx]?.ToString();
                        if (!DateTime.TryParse(dateStr, out rowDate)) continue;
                        if (startDate.HasValue && endDate.HasValue && (rowDate < startDate.Value || rowDate > endDate.Value)) continue;
                    }

                    var newRow = finalDt.NewRow();
                    foreach (var kvp in colMappings)
                    {
                        DataColumn targetCol = kvp.Key;
                        string val = rawRow[kvp.Value]?.ToString();

                        if (targetCol.DataType == typeof(DateTime))
                        {
                            if (kvp.Value == dateIdx) newRow[targetCol] = rowDate;
                            else if (DateTime.TryParse(val, out DateTime d)) newRow[targetCol] = d;
                        }
                        else
                        {
                            newRow[targetCol] = val;
                        }
                    }
                    finalDt.Rows.Add(newRow);
                }

                // 4. Sort
                if (!string.IsNullOrEmpty(dateColumn) && finalDt.Columns.Contains(dateColumn))
                {
                    finalDt.DefaultView.Sort = $"{dateColumn} ASC";
                    return finalDt.DefaultView.ToTable();
                }

                return finalDt;
            });
        }

        private double ParseDoubleSafe(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return 0;
            string strVal = val.Trim();

            int lastComma = strVal.LastIndexOf(',');
            int lastDot = strVal.LastIndexOf('.');

            if (lastComma > lastDot)
                strVal = strVal.Replace(".", "").Replace(",", ".");
            else if (lastDot > lastComma && lastComma != -1)
                strVal = strVal.Replace(",", "");
            else if (lastComma != -1 && lastDot == -1)
                strVal = strVal.Replace(",", ".");

            double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double res);
            return res;
        }

        public async Task<List<ErrorEventModel>> GetErrorDataAsync(DateTime startDate, DateTime endDate, string tableName)
        {
            return await Task.Run(() =>
            {
                var errorList = new List<ErrorEventModel>();
                var dt = GetOrLoadTable(tableName);

                if (dt == null || dt.Rows.Count == 0) return errorList;

                DataColumn colDate = null, colShift = null, colStopDuration = null;
                DataColumn colSavedBreak = null, colSavedMaint = null, colActualWork = null;
                var errorCols = new List<DataColumn>();

                foreach (DataColumn c in dt.Columns)
                {
                    string n = c.ColumnName.ToLowerInvariant().Trim();
                    bool hasKazanim = n.Contains("kazanım") || n.Contains("kazanim");
                    
                    if (n.Contains("tarih") || n == "date") colDate = c;
                    else if (n.Contains("vardiya") || n == "shift") colShift = c;
                    else if (n.Contains("duraklama") || n.Contains("stop")) colStopDuration = c;
                    // FIX: Columns detection aligned exactly with Online Repository
                    else if (n.Contains("engelemeyen") || (n.Contains("zaman") && hasKazanim && !n.Contains("mola"))) colSavedBreak = c;
                    else if ((n.Contains("mola") || n.Contains("bakım") || n.Contains("bakim") || n.Contains("mola/bakım") || n.Contains("mola / bakım")) && hasKazanim) colSavedMaint = c;
                    else if (n.Contains("fiili") || n.Contains("çalışılan") || n.Contains("calisilan") || n.Contains("work")) colActualWork = c;
                    else if (n.StartsWith("hata_kodu") || n.StartsWith("error_code") || n.StartsWith("code")) errorCols.Add(c);
                }

                if (colDate == null) return errorList;

                foreach (DataRow row in dt.Rows)
                {
                    string dateStr = row[colDate]?.ToString();
                    if (string.IsNullOrWhiteSpace(dateStr) || !DateTime.TryParse(dateStr, out DateTime date)) continue;
                    if (date.Date < startDate.Date || date.Date > endDate.Date) continue;

                    string shift = colShift != null ? row[colShift].ToString().Trim() : "Unknown";
                    string rowId = Guid.NewGuid().ToString();

                    double rowStopMin = 0, savedBreak = 0, savedMaint = 0, actualWork = 0;

                    if (colStopDuration != null)
                    {
                        string val = row[colStopDuration]?.ToString();
                        if (TimeSpan.TryParse(val, out TimeSpan ts)) rowStopMin = ts.TotalMinutes;
                        else if (DateTime.TryParse(val, out DateTime dVal)) rowStopMin = dVal.TimeOfDay.TotalMinutes;
                    }

                    // FIX: Implemented safe parsing to avoid 0 values from culture clashes
                    if (colSavedBreak != null) savedBreak = ParseDoubleSafe(row[colSavedBreak]?.ToString());
                    if (colSavedMaint != null) savedMaint = ParseDoubleSafe(row[colSavedMaint]?.ToString());

                    if (colActualWork != null)
                    {
                        string val = row[colActualWork]?.ToString();
                        if (TimeSpan.TryParse(val, out TimeSpan ts)) actualWork = ts.TotalMinutes;
                        else if (DateTime.TryParse(val, out DateTime dVal)) actualWork = dVal.TimeOfDay.TotalMinutes;
                        else actualWork = ParseDoubleSafe(val);
                    }

                    bool hasAnyErrors = false;
                    foreach (var errCol in errorCols)
                    {
                        string cellData = row[errCol]?.ToString();
                        if (string.IsNullOrWhiteSpace(cellData)) continue;

                        var model = ErrorEventModel.Parse(cellData, date, shift, rowStopMin, savedBreak, savedMaint, actualWork, rowId);
                        if (model != null)
                        {
                            errorList.Add(model);
                            hasAnyErrors = true;
                        }
                    }

                    if (!hasAnyErrors)
                    {
                        errorList.Add(new ErrorEventModel
                        {
                            Date = date,
                            Shift = shift,
                            UniqueRowId = rowId,
                            RowTotalStopMinutes = rowStopMin,
                            RowSavedTimeBreak = savedBreak,
                            RowSavedTimeMaint = savedMaint,
                            RowActualWorkingMinutes = actualWork,
                            ErrorDescription = "NO_ERROR",
                            DurationMinutes = 0
                        });
                    }
                }
                return errorList;
            });
        }

        // --- Interface Stubs ---
        public Task<bool> TableExistsAsync(string tableName) => Task.FromResult(File.Exists(Path.Combine(_folderPath, $"{tableName}.csv")));

        public Task<DataTable> GetSystemLogsAsync() => Task.FromResult(new DataTable());

        public Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName) => Task.FromResult((false, "Offline Mode"));

        public Task<bool> DeleteTableAsync(string tableName) => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName) => Task.FromResult((false, "Offline Mode"));

        public Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema) => Task.FromResult((false, "Offline Mode"));

        public Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data) => Task.FromResult((false, "Offline Mode"));

        public Task<bool> ClearSystemLogsAsync() => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> RenameColumnAsync(string tableName, string oldName, string newName) => Task.FromResult((false, "Offline Mode"));

        public Task<bool> ClearHierarchyMapForTableAsync(string tableName) => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData) => Task.FromResult((false, "Offline Mode"));

        public Task<string> GetActualColumnNameAsync(string t, string p1, string p2, string p3, string p4, string c) => Task.FromResult<string>(null);

        public Task<List<string>> GetDistinctPart1ValuesAsync(string t) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart2ValuesAsync(string t, string p1) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart3ValuesAsync(string t, string p1, string p2) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart4ValuesAsync(string t, string p1, string p2, string p3) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string t, string p1, string p2, string p3, string p4) => Task.FromResult(new List<string>());
    }
}