// In WPF_LoginForm.Models/DashboardSnapshot.cs
using System;
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    /// <summary>
    /// Represents a complete snapshot of the dashboard's state at a moment in time,
    /// including configurations, date range, and all series data.
    /// Used for the enhanced import/export functionality.
    /// </summary>
    public class DashboardSnapshot
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public List<DashboardConfiguration> Configurations { get; set; } = new List<DashboardConfiguration>();
        public List<ChartSeriesSnapshot> SeriesData { get; set; } = new List<ChartSeriesSnapshot>();
    }
}