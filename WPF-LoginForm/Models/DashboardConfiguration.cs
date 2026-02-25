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

        // --- NEW PROPERTY: PIVOT LOGIC ---
        /// <summary>
        /// Allows the chart to dynamically split data into multiple lines/bars based on categorical values.
        /// (e.g., Split Total Amount by "Type/TÜR" column)
        /// </summary>
        public string SplitByColumn { get; set; }

        public List<SeriesConfiguration> Series { get; set; } = new List<SeriesConfiguration>();

        public string AggregationType { get; set; } = "Daily";
    }
}