using System;

namespace WPF_LoginForm.ViewModels
{
    public class HelpViewModel : ViewModelBase
    {
        public string HelpContent { get; set; } = "Welcome to the Help Section.\n\n" +
                                                  "1. Dashboard: View charts and analytics.\n" +
                                                  "2. Reports: Manage data, filter, and export.\n" +
                                                  "3. Settings: Switch between SQL Server and PostgreSQL database providers.";
    }
}