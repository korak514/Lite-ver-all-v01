// Repositories/OfflineDataRepository.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        // Regex to split CSV by commas, but ignore commas inside quotes
        private readonly string _csvSplitPattern = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)";

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

        private DataTable ReadCsv(string filePath)
        {
            var dt = new DataTable();
            if (!File.Exists(filePath)) return dt;

            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return dt;

            var headers = Regex.Split(lines[0], _csvSplitPattern);
            foreach (var h in headers) dt.Columns.Add(h.Trim('"'));

            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var vals = Regex.Split(line, _csvSplitPattern);
                var row = dt.NewRow();
                for (int i = 0; i < Math.Min(vals.Length, dt.Columns.Count); i++)
                {
                    row[i] = vals[i].Trim('"');
                }
                dt.Rows.Add(row);
            }
            return dt;
        }

        public async Task<List<string>> GetTableNamesAsync()
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
            string path = Path.Combine(_folderPath, $"{tableName}.csv");
            var dt = await Task.Run(() => ReadCsv(path));
            dt.TableName = tableName;
            return (dt, true);
        }

        // --- FIX: Implement actual Date Range Scanning for Dashboard Sliders ---
        public async Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn)
        {
            try
            {
                var dtResult = await GetTableDataAsync(tableName, 0);
                var dt = dtResult.Data;

                if (dt != null && dt.Columns.Contains(dateColumn))
                {
                    var dates = dt.AsEnumerable()
                        .Select(r => r[dateColumn]?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Select(s => { DateTime.TryParse(s, out DateTime d); return d; })
                        .Where(d => d != DateTime.MinValue)
                        .ToList();

                    if (dates.Any())
                    {
                        return (dates.Min(), dates.Max());
                    }
                }
            }
            catch { }
            return (DateTime.Today.AddMonths(-1), DateTime.Today);
        }

        // --- FIX: Implement Data Filtering & Type Conversion for Dashboard Charts ---
        public async Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate)
        {
            string path = Path.Combine(_folderPath, $"{tableName}.csv");
            var rawDt = await Task.Run(() => ReadCsv(path));

            // HomeViewModel strictly expects the DateColumn to be typeof(DateTime), not string.
            // We must create a strongly typed table.
            var typedDt = new DataTable(tableName);
            foreach (var col in columns)
            {
                if (rawDt.Columns.Contains(col))
                {
                    typedDt.Columns.Add(col, col == dateColumn ? typeof(DateTime) : typeof(string));
                }
            }

            if (typedDt.Columns.Count == 0) return typedDt;

            foreach (DataRow row in rawDt.Rows)
            {
                var newRow = typedDt.NewRow();
                bool hasDate = true;
                DateTime rowDate = DateTime.MinValue;

                foreach (DataColumn col in typedDt.Columns)
                {
                    if (col.ColumnName == dateColumn)
                    {
                        if (DateTime.TryParse(row[col.ColumnName]?.ToString(), out DateTime d))
                        {
                            rowDate = d;
                            newRow[col] = d;
                        }
                        else hasDate = false;
                    }
                    else
                    {
                        newRow[col] = row[col.ColumnName];
                    }
                }

                if (hasDate || string.IsNullOrEmpty(dateColumn))
                {
                    if (startDate.HasValue && endDate.HasValue && hasDate)
                    {
                        if (rowDate >= startDate.Value && rowDate <= endDate.Value)
                            typedDt.Rows.Add(newRow);
                    }
                    else
                    {
                        typedDt.Rows.Add(newRow);
                    }
                }
            }

            // Sort ASC by Date for line charts
            if (!string.IsNullOrEmpty(dateColumn) && typedDt.Columns.Contains(dateColumn))
            {
                var view = typedDt.DefaultView;
                view.Sort = $"{dateColumn} ASC";
                return view.ToTable();
            }

            return typedDt;
        }

        // --- FIX: Implement full Analytics Parsing for Offline Mode ---
        public async Task<List<ErrorEventModel>> GetErrorDataAsync(DateTime startDate, DateTime endDate, string tableName)
        {
            var errorList = new List<ErrorEventModel>();
            var dtResult = await GetTableDataAsync(tableName, 0);
            var dt = dtResult.Data;

            if (dt == null || dt.Rows.Count == 0) return errorList;

            await Task.Run(() =>
            {
                DataColumn colDate = null, colShift = null, colStopDuration = null;
                DataColumn colSavedBreak = null, colSavedMaint = null;
                var errorCols = new List<DataColumn>();

                foreach (DataColumn c in dt.Columns)
                {
                    string n = c.ColumnName.ToLower();
                    if (n.Contains("tarih") || n == "date") colDate = c;
                    else if (n.Contains("vardiya") || n == "shift") colShift = c;
                    else if (n.Contains("duraklama") || n.Contains("stop")) colStopDuration = c;
                    else if (n.Contains("engelemeyen") || n.Contains("kazanımı")) colSavedBreak = c;
                    else if (n.Contains("mola") && n.Contains("bakım")) colSavedMaint = c;
                    else if (n.StartsWith("hata_kodu") || n.StartsWith("error_code") || n.StartsWith("code")) errorCols.Add(c);
                }

                if (colDate == null) return;

                foreach (DataRow row in dt.Rows)
                {
                    if (row[colDate] == DBNull.Value || string.IsNullOrWhiteSpace(row[colDate].ToString())) continue;

                    if (!DateTime.TryParse(row[colDate].ToString(), out DateTime date)) continue;
                    if (date < startDate || date > endDate) continue;

                    string shift = colShift != null ? row[colShift].ToString() : "Unknown";
                    string rowId = Guid.NewGuid().ToString();

                    double rowStopMin = 0;
                    if (colStopDuration != null && row[colStopDuration] != DBNull.Value)
                    {
                        string val = row[colStopDuration].ToString();
                        if (TimeSpan.TryParse(val, out TimeSpan ts)) rowStopMin = ts.TotalMinutes;
                        else if (DateTime.TryParse(val, out DateTime dVal)) rowStopMin = dVal.TimeOfDay.TotalMinutes;
                    }

                    double savedBreak = 0;
                    if (colSavedBreak != null && row[colSavedBreak] != DBNull.Value) double.TryParse(row[colSavedBreak].ToString(), out savedBreak);

                    double savedMaint = 0;
                    if (colSavedMaint != null && row[colSavedMaint] != DBNull.Value) double.TryParse(row[colSavedMaint].ToString(), out savedMaint);

                    foreach (var errCol in errorCols)
                    {
                        if (row[errCol] != DBNull.Value)
                        {
                            string cellData = row[errCol].ToString();
                            if (string.IsNullOrWhiteSpace(cellData)) continue;

                            var model = ErrorEventModel.Parse(cellData, date, shift, rowStopMin, savedBreak, savedMaint, rowId);
                            if (model != null) errorList.Add(model);
                        }
                    }
                }
            });

            return errorList;
        }

        public Task<bool> TableExistsAsync(string tableName) =>
            Task.FromResult(File.Exists(Path.Combine(_folderPath, $"{tableName}.csv")));

        public Task<DataTable> GetSystemLogsAsync() =>
            Task.FromResult(new DataTable());

        // --- ALL WRITE OPERATIONS BLOCKED IN OFFLINE MODE ---
        public Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName) =>
            Task.FromResult((false, "Application is in Offline Mode. Changes cannot be saved."));

        public Task<bool> DeleteTableAsync(string tableName) => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName) => Task.FromResult((false, "Offline Mode"));

        public Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema) => Task.FromResult((false, "Offline Mode"));

        public Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data) => Task.FromResult((false, "Offline Mode"));

        public Task<bool> ClearSystemLogsAsync() => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> RenameColumnAsync(string tableName, string oldName, string newName) => Task.FromResult((false, "Offline Mode"));

        public Task<bool> ClearHierarchyMapForTableAsync(string tableName) => Task.FromResult(false);

        public Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData) => Task.FromResult((false, "Offline Mode"));

        // --- HIERARCHY STUBS ---
        public Task<string> GetActualColumnNameAsync(string t, string p1, string p2, string p3, string p4, string c) => Task.FromResult<string>(null);

        public Task<List<string>> GetDistinctPart1ValuesAsync(string t) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart2ValuesAsync(string t, string p1) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart3ValuesAsync(string t, string p1, string p2) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctPart4ValuesAsync(string t, string p1, string p2, string p3) => Task.FromResult(new List<string>());

        public Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string t, string p1, string p2, string p3, string p4) => Task.FromResult(new List<string>());
    }
}