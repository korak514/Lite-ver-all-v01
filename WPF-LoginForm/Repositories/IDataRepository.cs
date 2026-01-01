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

        // --- OPTIMIZED: Data Retrieval now supports an optional limit ---
        /// <summary>
        /// Gets data from a table.
        /// </summary>
        /// <param name="limit">Number of rows to fetch (0 for all).</param>
        Task<DataTable> GetTableDataAsync(string tableName, int limit = 0);

        Task<DataTable> GetDataAsync(string tableName, List<string> columns, string dateColumn, DateTime? startDate, DateTime? endDate);

        // System Logs
        Task<DataTable> GetSystemLogsAsync();

        Task<bool> ClearSystemLogsAsync();

        // Distinct Values
        Task<List<string>> GetDistinctPart1ValuesAsync(string tableName);

        Task<List<string>> GetDistinctPart2ValuesAsync(string tableName, string p1);

        Task<List<string>> GetDistinctPart3ValuesAsync(string tableName, string p1, string p2);

        Task<List<string>> GetDistinctPart4ValuesAsync(string tableName, string p1, string p2, string p3);

        Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string tableName, string p1, string p2, string p3, string p4);

        // Hierarchy Map
        Task<bool> ClearHierarchyMapForTableAsync(string tableName);

        Task<(bool Success, string ErrorMessage)> ImportHierarchyMapAsync(DataTable mapData);

        // Write Operations
        Task<(bool Success, string ErrorMessage)> SaveChangesAsync(DataTable changes, string tableName);

        Task<bool> DeleteTableAsync(string tableName);

        Task<(bool Success, string ErrorMessage)> AddPrimaryKeyAsync(string tableName);

        // Bulk / Creation
        Task<(bool Success, string ErrorMessage)> CreateTableAsync(string tableName, List<ColumnSchemaViewModel> schema);

        Task<(bool Success, string ErrorMessage)> BulkImportDataAsync(string tableName, DataTable data);
    }
}