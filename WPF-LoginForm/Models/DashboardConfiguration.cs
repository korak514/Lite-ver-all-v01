// Models/DashboardConfiguration.cs
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class DashboardConfiguration
    {
        public int ChartPosition { get; set; }
        public bool IsEnabled { get; set; } = true;
        public string TableName { get; set; }
        public string ChartType { get; set; } = "Line";

        public string DataStructureType { get; set; } = "Daily Date";

        public int RowsToIgnore { get; set; } = 0;

        /// <summary>
        /// If true, numbers are parsed using invariant culture ('.' decimal, ',' thousands).
        /// If false, uses the application's current culture (e.g., tr-TR with ',' decimal).
        /// </summary>
        public bool UseInvariantCultureForNumbers { get; set; } = false;

        public string DateColumn { get; set; }

        public string SplitByColumn { get; set; }

        public List<SeriesConfiguration> Series { get; set; } = new List<SeriesConfiguration>();

        public string AggregationType { get; set; } = "Daily";

        // --- NEW: Value Aggregation (Sum vs Average) ---
        public string ValueAggregation { get; set; } = "Sum";

        // --- PROPERTIES: LABEL & PIVOT CONTROL ---
        public bool HideNumbersInLabels { get; set; } = false;

        public bool SimplifyLabels { get; set; } = false;
        public List<string> SelectedSplitCategories { get; set; } = new List<string>();
    }
}