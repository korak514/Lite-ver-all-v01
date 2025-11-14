// In WPF_LoginForm.Models/ImportSettings.cs

namespace WPF_LoginForm.Models
{
    /// <summary>
    /// A simple data model to hold the settings selected by the user
    /// in the advanced import dialog.
    /// </summary>
    public class ImportSettings
    {
        public string FilePath { get; set; }
        public int RowsToIgnore { get; set; }
    }
}