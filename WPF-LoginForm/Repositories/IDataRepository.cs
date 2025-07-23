using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace WPF_LoginForm.Repositories
{
    public interface IDataRepository
    {
        Task<List<string>> GetTableNamesAsync();
        Task<DataTable> GetTableDataAsync(string tableName);
        Task<bool> SaveChangesAsync(DataTable changes, string tableName);

        // --- NEW METHODS FOR ColumnHierarchyMap ---
        /// <summary>
        /// Gets distinct Part1 values for a given owning table from ColumnHierarchyMap.
        /// </summary>
        Task<List<string>> GetDistinctPart1ValuesAsync(string owningTableName);

        /// <summary>
        /// Gets distinct Part2 values based on the selected Part1 for a given owning table.
        /// </summary>
        Task<List<string>> GetDistinctPart2ValuesAsync(string owningTableName, string part1Value);

        /// <summary>
        /// Gets distinct Part3 values based on the selected Part1 and Part2 for a given owning table.
        /// </summary>
        Task<List<string>> GetDistinctPart3ValuesAsync(string owningTableName, string part1Value, string part2Value);

        /// <summary>
        /// Gets distinct Part4 values based on the selected Part1, Part2, and Part3 for a given owning table.
        /// </summary>
        Task<List<string>> GetDistinctPart4ValuesAsync(string owningTableName, string part1Value, string part2Value, string part3Value);

        /// <summary>
        /// Gets distinct CoreItemDisplayNames based on the full selected path of Part1-Part4 for a given owning table.
        /// </summary>
        Task<List<string>> GetDistinctCoreItemDisplayNamesAsync(string owningTableName, string part1Value, string part2Value, string part3Value, string part4Value);

        /// <summary>
        /// Gets the actual database column name from ColumnHierarchyMap based on the full selected path.
        /// </summary>
        Task<string> GetActualColumnNameAsync(string owningTableName,
                                            string part1Value,
                                            string part2Value, // Can be null if not applicable
                                            string part3Value, // Can be null if not applicable
                                            string part4Value, // Can be null if not applicable
                                            string coreItemDisplayName);

        Task<DataTable> GetDataAsync(string tableName, string xAxisColumn, List<string> yAxisColumns, DateTime startDate, DateTime endDate);
    }
}