// Services/DataImportExportHelper.cs

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OfficeOpenXml;

namespace WPF_LoginForm.Services
{
    public static class DataImportExportHelper
    {
        public class ImportResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int Added { get; set; }
            public int Skipped { get; set; }
        }

        public static string BuildFilterString(DataTable table, string text, string colName, bool global, bool hasDate, string dateCol, DateTime? start, DateTime? end)
        {
            if (table == null) return "";
            var filters = new List<string>();

            // 1. Text Filter
            if (!string.IsNullOrWhiteSpace(text))
            {
                string safe = text.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]");
                if (global)
                {
                    var sub = table.Columns.Cast<DataColumn>().Select(c => $"Convert([{c.ColumnName}], 'System.String') LIKE '%{safe}%'");
                    if (sub.Any()) filters.Add($"({string.Join(" OR ", sub)})");
                }
                else if (!string.IsNullOrEmpty(colName) && table.Columns.Contains(colName))
                {
                    string safeCol = colName.Replace("]", "]]");
                    // Check numeric operators
                    string opFilter = "";
                    if (text.Trim().StartsWith(">") || text.Trim().StartsWith("<"))
                    {
                        var col = table.Columns[colName];
                        var op = text.Trim().StartsWith(">=") || text.Trim().StartsWith("<=") ? text.Trim().Substring(0, 2) : text.Trim().Substring(0, 1);
                        if (double.TryParse(text.Trim().Substring(op.Length), NumberStyles.Any, CultureInfo.CurrentCulture, out double val))
                        {
                            if (col.DataType == typeof(int) || col.DataType == typeof(double) || col.DataType == typeof(decimal))
                                opFilter = $"[{safeCol}] {op} {val.ToString(CultureInfo.InvariantCulture)}";
                        }
                    }
                    filters.Add(string.IsNullOrEmpty(opFilter) ? $"Convert([{safeCol}], 'System.String') LIKE '%{safe}%'" : opFilter);
                }
            }

            // 2. Date Filter
            if (hasDate && !string.IsNullOrEmpty(dateCol) && start.HasValue && end.HasValue && table.Columns.Contains(dateCol))
            {
                string safeD = dateCol.Replace("]", "]]");
                filters.Add($"[{safeD}] >= #{start.Value:MM/dd/yyyy}# AND [{safeD}] <= #{end.Value:MM/dd/yyyy}#");
            }

            return string.Join(" AND ", filters);
        }

        public static ImportResult ImportDataToTable(string filePath, DataTable targetTable, int rowsToIgnore)
        {
            var res = new ImportResult();
            var errors = new List<string>();
            DataTable importDt = null;

            try
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == ".xlsx") importDt = LoadXlsx(filePath, errors, rowsToIgnore);
                else if (ext == ".csv") importDt = LoadCsv(filePath, errors, rowsToIgnore);

                if (importDt != null && importDt.Rows.Count > 0)
                {
                    foreach (DataRow sRow in importDt.Rows)
                    {
                        try
                        {
                            var newRow = targetTable.NewRow();
                            foreach (DataColumn tCol in targetTable.Columns)
                            {
                                if (tCol.ColumnName.Equals("ID", StringComparison.OrdinalIgnoreCase)) continue; // Skip ID

                                // Match by name (insensitive)
                                var sCol = importDt.Columns.Cast<DataColumn>().FirstOrDefault(c => c.ColumnName.Equals(tCol.ColumnName, StringComparison.OrdinalIgnoreCase));
                                if (sCol != null)
                                {
                                    object val = sRow[sCol];
                                    if (val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString()))
                                    {
                                        if (!tCol.AllowDBNull) throw new Exception("Required");
                                        newRow[tCol] = DBNull.Value;
                                    }
                                    else
                                    {
                                        // Type conversions
                                        if (tCol.DataType == typeof(DateTime)) newRow[tCol] = ParseDate(val) ?? (object)DBNull.Value;
                                        else if (tCol.DataType == typeof(bool)) newRow[tCol] = ParseBool(val.ToString());
                                        else if (tCol.DataType == typeof(Guid)) newRow[tCol] = Guid.Parse(val.ToString());
                                        else newRow[tCol] = Convert.ChangeType(val, tCol.DataType, CultureInfo.CurrentCulture);
                                    }
                                }
                            }
                            targetTable.Rows.Add(newRow);
                            res.Added++;
                        }
                        catch { res.Skipped++; }
                    }
                    res.Success = true;
                    res.Message = $"Imported: {res.Added}, Skipped: {res.Skipped}.";
                }
                else res.Message = "No data found or file empty.";
            }
            catch (Exception ex) { res.Message = $"File Error: {ex.Message}"; }
            return res;
        }

        public static void ExportTable(string path, DataTable table, string sheetName)
        {
            var rows = table.AsEnumerable().Where(r => r.RowState != DataRowState.Deleted);
            if (path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
                using (var p = new ExcelPackage(new FileInfo(path)))
                {
                    var ws = p.Workbook.Worksheets.Add(Sanitize(sheetName));
                    if (rows.Any())
                    {
                        ws.Cells["A1"].LoadFromDataTable(rows.CopyToDataTable(), true);
                        ws.Cells.AutoFitColumns();
                    }
                    p.Save();
                }
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Quote(c.ColumnName))));
                foreach (var r in rows) sb.AppendLine(string.Join(",", r.ItemArray.Select(i => Quote(i?.ToString()))));
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
        }

        private static DataTable LoadXlsx(string path, List<string> err, int skip)
        {
            var dt = new DataTable();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            // Open with FileShare.ReadWrite to prevent crash if file is open in Excel
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var p = new ExcelPackage(stream))
            {
                var ws = p.Workbook.Worksheets.FirstOrDefault();
                if (ws == null || ws.Dimension == null) return null;
                int start = 1 + skip;
                for (int c = 1; c <= ws.Dimension.End.Column; c++)
                {
                    string h = ws.Cells[start, c].Text.Trim();
                    dt.Columns.Add(string.IsNullOrEmpty(h) ? $"Col{c}" : h);
                }
                for (int r = start + 1; r <= ws.Dimension.End.Row; r++)
                {
                    var row = dt.NewRow();
                    bool hasVal = false;
                    for (int c = 1; c <= dt.Columns.Count; c++)
                    {
                        var val = ws.Cells[r, c].Value;
                        if (val != null) hasVal = true;
                        row[c - 1] = val ?? DBNull.Value;
                    }
                    if (hasVal) dt.Rows.Add(row);
                }
            }
            return dt;
        }

        private static DataTable LoadCsv(string path, List<string> err, int skip)
        {
            var dt = new DataTable();

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                // FIX (Bug 3): Prevent OutOfMemoryException by processing the file line-by-line instead of loading everything into memory at once
                string pattern = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)"; // CSV Split regex

                // Skip requested lines
                for (int i = 0; i < skip; i++)
                {
                    if (reader.ReadLine() == null) return dt;
                }

                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine)) return dt;

                var headers = Regex.Split(headerLine, pattern);
                foreach (var h in headers) dt.Columns.Add(h.Trim('"'));

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var row = dt.NewRow();
                    var vals = Regex.Split(line, pattern);
                    for (int i = 0; i < Math.Min(vals.Length, dt.Columns.Count); i++)
                    {
                        row[i] = vals[i].Trim('"');
                    }
                    dt.Rows.Add(row);
                }
            }
            return dt;
        }

        private static DateTime? ParseDate(object v)
        {
            if (v == null) return null;
            if (v is DateTime d) return d;
            if (v is double dbl) return DateTime.FromOADate(dbl);
            if (DateTime.TryParse(v.ToString(), CultureInfo.CurrentCulture, DateTimeStyles.None, out DateTime dt)) return dt;
            if (DateTime.TryParse(v.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private static bool ParseBool(string v) => v?.ToLower() == "true" || v == "1" || v?.ToLower() == "yes" || v?.ToLower() == "on";

        private static string Quote(string v) => (v?.Contains(",") == true || v?.Contains("\"") == true) ? $"\"{v.Replace("\"", "\"\"")}\"" : v;

        private static string Sanitize(string s) => Regex.Replace(s ?? "Sheet1", @"[\\/\?\*\[\]:]", "_");
    }
}