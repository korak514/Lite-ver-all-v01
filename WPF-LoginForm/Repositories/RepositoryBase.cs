using System.Configuration; // Add this using
using Microsoft.Data.SqlClient; // Keep or add if needed for SqlConnection type

namespace WPF_LoginForm.Repositories
{
    public abstract class RepositoryBase
    {
        private readonly string _connectionString;
        public RepositoryBase()
        {
            // Read connection string from App.config by name
            _connectionString = ConfigurationManager.ConnectionStrings["LoginDbConnection"]?.ConnectionString;

            // Optional: Add a check if the connection string is null/empty
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new ConfigurationErrorsException("Connection string 'LoginDbConnection' not found or empty in App.config.");
            }
        }
        protected SqlConnection GetConnection()
        {
            // Consider adding error handling if _connectionString was null
            return new SqlConnection(_connectionString);
        }
    }
}