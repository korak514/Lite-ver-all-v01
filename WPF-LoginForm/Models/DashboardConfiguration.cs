// In WPF_LoginForm.Models/DashboardConfiguration.cs
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

        // --- NEW PROPERTY ---
        /// <summary>
        /// If true, numbers are parsed using invariant culture ('.' decimal, ',' thousands).
        /// If false, uses the application's current culture (e.g., tr-TR with ',' decimal).
        /// </summary>
        public bool UseInvariantCultureForNumbers { get; set; } = false;

        public string DateColumn { get; set; }

        public List<SeriesConfiguration> Series { get; set; } = new List<SeriesConfiguration>();

        public string AggregationType { get; set; } = "Daily";
    }
}