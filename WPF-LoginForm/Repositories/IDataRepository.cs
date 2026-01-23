using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using WPF_LoginForm.Models;
using WPF_LoginForm.ViewModels;

namespace WPF_LoginForm.Repositories
{
    public interface IDataRepository
    {
        // Schema & Metadata
        Task<List<string>> GetTableNamesAsync();

        Task<bool> TableExistsAsync(string tableName);

        Task<string> GetActualColumnNameAsync(string tableName, string p1, string p2, string p3, string p4, string coreItem);

        Task<(DateTime Min, DateTime Max)> GetDateRangeAsync(string tableName, string dateColumn);

        Task<(bool Success, string ErrorMessage)> RenameColumnAsync(string tableName, string oldName, string newName);

        // Data Retrieval
        Task<(DataTable Data, bool IsSortable)> GetTableDataAsync(string tableName, int limit = 0);

        Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate);

        // NEW: Error Analytics
        Task<List<ErrorEventModel>> GetErrorDataAsync(DateTime startDate, DateTime endDate, string tableName);

        // System Logs
        Task<DataTable> GetSystemLogsAsync();

        Task<bool> ClearSystemLogsAsync();

        // Hierarchy Dropdowns
        Task<List<string>> GetDistinctPart1ValuesAsync(string tableName);

        Task<List<string>> GetDistinctPart2ValuesAsync(string tableName, string p1);

        Task<List<string>> GetDistinctPart3ValuesAsync(string tableName, string p1, string p2);

        Task<List<string>> GetDistinctPart4ValuesAsync(string tableName, string p1, string p2, string p3);

        Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string tableName, string p1, string p2, string p3, string p4);

        // Hierarchy Management
        Task<bool> ClearHierarchyMapForTableAsync(string tableName);

        Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData);

        // Write Operations
        Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName);

        Task<bool> DeleteTableAsync(string tableName);

        Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName);

        // Creation & Import
        Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema);

        Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data);
    }
}