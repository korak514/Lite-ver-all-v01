// In WPF_LoginForm.Repositories/IDataRepository.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public interface IDataRepository
    {
        Task<List<string>> GetTableNamesAsync();
        Task<DataTable> GetTableDataAsync(string tableName);
        Task<bool> SaveChangesAsync(DataTable changes, string tableName);

        Task<List<string>> GetDistinctPart1ValuesAsync(string owningTableName);
        Task<List<string>> GetDistinctPart2ValuesAsync(string owningTableName, string part1Value);
        Task<List<string>> GetDistinctPart3ValuesAsync(string owningTableName, string part1Value, string part2Value);
        Task<List<string>> GetDistinctPart4ValuesAsync(string owningTableName, string part1Value, string part2Value, string part3Value);
        Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string owningTableName, string part1Value, string part2Value, string part3Value, string part4Value);
        Task<string> GetActualColumnNameAsync(string owningTableName, string part1Value, string part2Value, string part3Value, string part4Value, string coreItemDisplayName);

        // --- MODIFIED METHOD SIGNATURE ---
        Task<DataTable> GetDataAsync(string tableName, List<string> columnsToSelect, string dateColumn, DateTime? startDate, DateTime? endDate);

        Task<(DateTime MinDate, DateTime MaxDate)> GetDateRangeAsync(string tableName, string dateColumnName);
        Task<bool> DeleteTableAsync(string tableName);

        Task<bool> TableExistsAsync(string tableName);
        Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema);
        Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data);
    }
}