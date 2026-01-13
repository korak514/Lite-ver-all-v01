using System.Data.SqlClient;
using WPF_LoginForm.Properties; // Access Settings directly
using WPF_LoginForm.Services.Database; // Access your Factory

namespace WPF_LoginForm.Repositories
{
    public abstract class RepositoryBase
    {
        // REMOVED: The logic reading from ConfigurationManager

        // ADDED: Logic to read from the dynamic user settings
        protected SqlConnection GetConnection()
        {
            // Option A: Use the existing Factory (Recommended for consistency)
            return (SqlConnection)DbConnectionFactory.GetConnection(ConnectionTarget.Auth);

            // OR Option B: Read directly from Settings (if you want to bypass the factory)
            // return new SqlConnection(Settings.Default.SqlAuthConnString);
        }
    }
}