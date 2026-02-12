using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Services
{
    public class ExcelAnalysisService
    {
        public async Task<List<string>> GetWorksheetNamesAsync(string filePath)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.", filePath);

            // Force Non-Commercial License to avoid EPPlus errors
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            return await Task.Run(() =>
            {
                var names = new List<string>();
                try
                {
                    // Open with FileShare.ReadWrite to allow reading even if Excel has it open
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var package = new ExcelPackage(stream))
                    {
                        foreach (var worksheet in package.Workbook.Worksheets)
                        {
                            names.Add(worksheet.Name);
                        }
                    }
                }
                catch (IOException)
                {
                    throw new IOException($"The file '{Path.GetFileName(filePath)}' is locked by another process.\nPlease close Excel and try again.");
                }
                return names;
            });
        }

        public async Task<List<ColumnSchemaViewModel>> AnalyzeFileAsync(string filePath, string worksheetName, int headerRow)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found.", filePath);

            return await Task.Run(() =>
            {
                var schemaList = new List<ColumnSchemaViewModel>();
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets[worksheetName];
                        if (worksheet == null || worksheet.Dimension == null)
                            throw new DataException($"The worksheet '{worksheetName}' is empty.");

                        int totalColumns = worksheet.Dimension.End.Column;
                        var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        // 1. Extract Headers
                        for (int col = 1; col <= totalColumns; col++)
                        {
                            string header = worksheet.Cells[headerRow, col].Text.Trim();
                            if (string.IsNullOrEmpty(header)) header = $"Column{col}";

                            // Handle duplicates (e.g., "Date", "Date") -> "Date", "Date_1"
                            string originalHeader = header;
                            int dupCount = 1;
                            while (usedHeaders.Contains(header))
                            {
                                header = $"{originalHeader}_{dupCount++}";
                            }
                            usedHeaders.Add(header);

                            schemaList.Add(new ColumnSchemaViewModel
                            {
                                SourceColumnName = header,
                                DestinationColumnName = SanitizeSqlName(header)
                            });
                        }

                        // 2. Smart Type Guessing (Scan up to 500 rows)
                        int startRow = headerRow + 1;
                        int endRow = Math.Min(worksheet.Dimension.End.Row, startRow + 500);

                        for (int i = 0; i < schemaList.Count; i++)
                        {
                            var samples = new List<string>();

                            // Collect samples
                            for (int r = startRow; r <= endRow; r++)
                            {
                                var cellValue = worksheet.Cells[r, i + 1].Text;
                                if (!string.IsNullOrWhiteSpace(cellValue))
                                {
                                    samples.Add(cellValue);
                                }
                            }

                            // Determine type based on majority consensus
                            schemaList[i].SelectedDataType = GuessDataType(samples);
                        }
                    }
                }
                catch (IOException)
                {
                    throw new IOException($"The file '{Path.GetFileName(filePath)}' is locked. Please close it.");
                }

                return schemaList;
            });
        }

        private string GuessDataType(List<string> data)
        {
            if (data == null || !data.Any()) return "Text (string)";

            int total = data.Count;
            // Threshold: 90% of data must match the type to classify it as such
            double threshold = 0.9;

            var culture = CultureInfo.CurrentCulture;
            var invariant = CultureInfo.InvariantCulture;

            // 1. Check Date
            int dateCount = data.Count(s =>
                DateTime.TryParse(s, culture, DateTimeStyles.None, out _) ||
                DateTime.TryParse(s, invariant, DateTimeStyles.None, out _));

            if ((double)dateCount / total > threshold) return "Date (datetime)";

            // 2. Check Integer (strict)
            int intCount = data.Count(s => long.TryParse(s.Trim(), NumberStyles.Integer, culture, out _));
            if ((double)intCount / total > threshold) return "Number (int)";

            // 3. Check Decimal
            int decimalCount = data.Count(s =>
                decimal.TryParse(s.Trim(), NumberStyles.Any, culture, out _) ||
                decimal.TryParse(s.Trim(), NumberStyles.Any, invariant, out _));

            if ((double)decimalCount / total > threshold) return "Decimal (decimal)";

            // Default
            return "Text (string)";
        }

        private string SanitizeSqlName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "Column_X";

            // Replace non-alphanumeric characters with underscores
            string sanitized = Regex.Replace(rawName, @"[^\w]", "_");

            // SQL columns cannot start with a digit
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            // Remove consecutive underscores for cleanliness
            sanitized = Regex.Replace(sanitized, @"_{2,}", "_");

            // Trim underscores from ends
            return sanitized.Trim('_');
        }
    }
}