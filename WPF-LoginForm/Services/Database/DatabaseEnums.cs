namespace WPF_LoginForm.Services.Database
{
    public enum DatabaseType
    {
        SqlServer,
        PostgreSql
    }

    // NEW: Defines which database to connect to
    public enum ConnectionTarget
    {
        Auth, // Connects to 'LoginDb' (Users, Logs)
        Data  // Connects to 'BusinessDb' (TestDT, Inventory, Customers)
    }
}