using System.Collections.ObjectModel;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.ViewModels
{
    public class HelpViewModel : ViewModelBase
    {
        public ObservableCollection<HelpTopic> HelpTopics { get; set; }

        public HelpViewModel()
        {
            HelpTopics = new ObservableCollection<HelpTopic>
            {
                new HelpTopic
                {
                    Title = "1. Basic Data Operations (Reports Tab)",
                    Content = "• Add: Inserts a new empty row or opens the Data Entry window.\n" +
                              "• Edit: Click 'Edit' to unlock the selected row. ID columns cannot be changed.\n" +
                              "• Delete: Removes the selected row(s) immediately.\n" +
                              "• Save: Commits your changes to the database. Always Save after Adding or Editing!\n" +
                              "• Reload: Refreshes data from the server. Discards unsaved changes."
                },
                new HelpTopic
                {
                    Title = "2. Special Feature: '_Long_' Tables & Hierarchy",
                    Content = "• If a table name starts with '_Long_' (case-insensitive), the app treats it as a Hierarchy Table.\n" +
                              "• Instead of a standard grid row, a special 'Detailed Entry Window' will open.\n" +
                              "• This window allows you to select Category > SubCategory > Item using dropdowns.\n" +
                              "• Requires the 'ColumnHierarchyMap' to be configured via the 'Import Map' button."
                },
                new HelpTopic
                {
                    Title = "3. Importing Data (Excel/CSV)",
                    Content = "• Import (Standard): Quickly loads data from Excel. Headers must match Database columns exactly.\n" +
                              "• Import Table (Advanced): Allows you to map columns and ignore header rows (e.g., skip first 3 rows).\n" +
                              "• Create Table: upload an Excel file, and the app will automatically create a SQL table based on the Excel columns."
                },
                new HelpTopic
                {
                    Title = "4. Hierarchy Mapping (For Dropdowns)",
                    Content = "• To make the '_Long_' table dropdowns work, you must define the logic.\n" +
                              "• Enable 'Show Adv. Import' checkbox in the toolbar.\n" +
                              "• Click 'Import Map'.\n" +
                              "• Download the Template, fill it with your Category/Product relationships, and Import it back."
                },
                new HelpTopic
                {
                    Title = "5. Search & Filtering Tricks",
                    Content = "• Text Search: Type any text to find partial matches.\n" +
                              "• Numeric Filter: Type '> 50' to find values greater than 50, or '< 100' for less than 100.\n" +
                              "• Date Filter: Click the Calendar icon to show the Date Range Slider.\n" +
                              "• NOTE: Filtering happens on loaded data. Use 'Load Full History' to search old records."
                },
                new HelpTopic
                {
                    Title = "6. Performance & Network",
                    Content = "• Default Limit: To keep the app fast, only the recent 500 rows are loaded initially.\n" +
                              "• Load All: Check 'Load Full History' to fetch everything (may be slow on Wi-Fi).\n" +
                              "• Retrying: If the network drops, the app will auto-retry 3 times. Look for the yellow status message."
                },
                new HelpTopic
                {
                    Title = "7. Settings & Admin",
                    Content = "• Admin users can manage other users in the Settings tab.\n" +
                              "• Backup Server: In Settings, you can define a 'Computer Name' to use if the IP address changes.\n" +
                              "• Timeout: Increase the 'Connection Timeout' if you have a very slow VPN connection."
                }
            };
        }
    }
}