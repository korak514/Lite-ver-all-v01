// Models/DashboardSnapshot.cs
using System;
using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class DashboardSnapshot
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Advanced Filters
        public bool IsFilterByDate { get; set; } = true;

        public bool IgnoreNonDateData { get; set; } = true;
        public bool UseIdToDateConversion { get; set; } = false;
        public DateTime InitialDateForConversion { get; set; } = DateTime.Today;

        // --- NEW: Global Setting for Label Overlap Control ---
        public bool GlobalIgnoreAfterHyphen { get; set; } = false;

        public List<DashboardConfiguration> Configurations { get; set; } = new List<DashboardConfiguration>();
        public List<ChartSeriesSnapshot> SeriesData { get; set; } = new List<ChartSeriesSnapshot>();
    }
}