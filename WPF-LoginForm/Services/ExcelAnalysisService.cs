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

            try
            {
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
            }
            catch (IOException)
            {
                // FIX: Handle file locking gracefully
                throw new IOException($"The file '{Path.GetFileName(filePath)}' is currently open in another program.\nPlease close it and try again.");
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

            try
            {
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = await Task.Run(() => package.Workbook.Worksheets[worksheetName]);
                    if (worksheet == null || worksheet.Dimension == null)
                    {
                        throw new DataException($"The worksheet '{worksheetName}' is empty or could not be found.");
                    }

                    int totalColumns = worksheet.Dimension.End.Column;
                    var usedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    for (int col = 1; col <= totalColumns; col++)
                    {
                        string header = worksheet.Cells[headerRow, col].Text.Trim();
                        if (string.IsNullOrEmpty(header))
                        {
                            header = $"Column{col}";
                        }

                        string originalHeader = header;
                        int duplicateCount = 1;
                        while (usedHeaders.Contains(header))
                        {
                            header = $"{originalHeader}_{duplicateCount}";
                            duplicateCount++;
                        }
                        usedHeaders.Add(header);

                        schemaList.Add(new ColumnSchemaViewModel
                        {
                            SourceColumnName = header,
                            DestinationColumnName = SanitizeSqlName(header)
                        });
                    }

                    int firstDataRowIndex = headerRow + 1;
                    bool dataFound = false;
                    int maxScanRow = Math.Min(worksheet.Dimension.End.Row, headerRow + 20);

                    for (int rowIndex = headerRow + 1; rowIndex <= maxScanRow; rowIndex++)
                    {
                        for (int colIndex = 1; colIndex <= totalColumns; colIndex++)
                        {
                            if (!string.IsNullOrWhiteSpace(worksheet.Cells[rowIndex, colIndex].Text))
                            {
                                firstDataRowIndex = rowIndex;
                                dataFound = true;
                                break;
                            }
                        }
                        if (dataFound) break;
                    }

                    if (!dataFound) firstDataRowIndex = headerRow + 1;

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
            }
            catch (IOException)
            {
                // FIX: Handle file locking gracefully
                throw new IOException($"The file '{Path.GetFileName(filePath)}' is currently open in another program.\nPlease close it and try again.");
            }

            return schemaList;
        }

        private string GuessDataType(List<string> data)
        {
            if (data == null || !data.Any())
            {
                return "Text (string)";
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