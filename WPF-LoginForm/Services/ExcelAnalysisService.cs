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
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified file does not exist.", filePath);
            }

            var names = new List<string>();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var fileInfo = new FileInfo(filePath);

            using (var package = new ExcelPackage(fileInfo))
            {
                await Task.Run(() =>
                {
                    foreach (var worksheet in package.Workbook.Worksheets)
                    {
                        names.Add(worksheet.Name);
                    }
                });
            }
            return names;
        }

        public async Task<List<ColumnSchemaViewModel>> AnalyzeFileAsync(string filePath, string worksheetName, int headerRow)
        {
            var schemaList = new List<ColumnSchemaViewModel>();
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("The specified file does not exist.", filePath);
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var fileInfo = new FileInfo(filePath);

            using (var package = new ExcelPackage(fileInfo))
            {
                var worksheet = await Task.Run(() => package.Workbook.Worksheets[worksheetName]);
                if (worksheet == null || worksheet.Dimension == null)
                {
                    throw new DataException($"The worksheet '{worksheetName}' is empty or could not be found.");
                }

                int totalColumns = worksheet.Dimension.End.Column;

                for (int col = 1; col <= totalColumns; col++)
                {
                    string header = worksheet.Cells[headerRow, col].Text.Trim();
                    if (string.IsNullOrEmpty(header))
                    {
                        header = $"Column{col}";
                    }

                    schemaList.Add(new ColumnSchemaViewModel
                    {
                        SourceColumnName = header,
                        DestinationColumnName = SanitizeSqlName(header)
                    });
                }

                // --- NEW LOGIC: Find the first row that actually contains data ---
                int firstDataRowIndex = headerRow + 1; // Start looking from the row after the header
                bool dataFound = false;
                for (int rowIndex = headerRow + 1; rowIndex <= worksheet.Dimension.End.Row; rowIndex++)
                {
                    // Check if any cell in this row contains a numerical value.
                    // This helps skip over rows with units or other text-only info.
                    for (int colIndex = 1; colIndex <= totalColumns; colIndex++)
                    {
                        if (decimal.TryParse(worksheet.Cells[rowIndex, colIndex].Text, out _))
                        {
                            firstDataRowIndex = rowIndex;
                            dataFound = true;
                            break; // Exit the inner column loop
                        }
                    }
                    if (dataFound)
                    {
                        break; // Exit the outer row loop
                    }
                }

                // If no numeric data is found at all, we still start after the header to be safe.
                if (!dataFound) firstDataRowIndex = headerRow + 1;
                // --- END OF NEW LOGIC ---

                // Scan up to 50 rows for a more reliable analysis, starting from the first real data row
                int scanLimit = Math.Min(worksheet.Dimension.End.Row, firstDataRowIndex + 50);
                for (int i = 0; i < schemaList.Count; i++)
                {
                    var columnData = new List<string>();
                    for (int row = firstDataRowIndex; row <= scanLimit; row++)
                    {
                        var cellValue = worksheet.Cells[row, i + 1].Text;
                        if (!string.IsNullOrWhiteSpace(cellValue))
                        {
                            columnData.Add(cellValue);
                        }
                    }
                    schemaList[i].SelectedDataType = GuessDataType(columnData);
                }
            }

            return schemaList;
        }

        private string GuessDataType(List<string> data)
        {
            if (data == null || !data.Any())
            {
                return "Text (string)"; // Default if no data to analyze
            }

            var culture = CultureInfo.CurrentCulture;
            bool allAreDate = data.All(s => DateTime.TryParse(s, culture, DateTimeStyles.None, out _));
            if (allAreDate)
            {
                return "Date (datetime)";
            }

            bool allAreInt = data.All(s => long.TryParse(s.Trim(), NumberStyles.Integer, culture, out _));
            if (allAreInt)
            {
                return "Number (int)";
            }

            bool allAreDecimal = data.All(s => decimal.TryParse(s.Trim(), NumberStyles.Number, culture, out _));
            if (allAreDecimal)
            {
                return "Decimal (decimal)";
            }

            return "Text (string)";
        }

        private string SanitizeSqlName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "Unnamed_Column";
            string sanitized = Regex.Replace(rawName, @"[^\w]", "_");
            if (char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }
            return sanitized;
        }
    }
}