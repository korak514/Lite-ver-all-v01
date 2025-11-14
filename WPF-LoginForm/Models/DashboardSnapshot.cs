// In WPF_LoginForm.Models/ChartSeriesSnapshot.cs
using System;
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    /// <summary>
    /// Represents a snapshot of a single data series from a chart,
    /// including its data points and visual styling for import/export.
    /// </summary>
    public class ChartSeriesSnapshot
    {
        // The position of the chart this series belongs to (e.g., 1 through 5)
        public int ChartPosition { get; set; }

        // The title of the series (e.g., "Sales", "Inventory")
        public string SeriesTitle { get; set; }

        // The color of the series, stored as a hex string (e.g., "#FF0078D7")
        public string HexColor { get; set; }

        // The actual data points for the series. Stored as a list of objects
        // to accommodate different data types (like DateTimePoint, double, etc.).
        public List<object> DataPoints { get; set; } = new List<object>();
    }
}