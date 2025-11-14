// In WPF_LoginForm.Models/SeriesConfiguration.cs
using System;

namespace WPF_LoginForm.Models
{
    public class SeriesConfiguration
    {
        // A unique identifier for this series within its chart configuration.
        public Guid Id { get; set; } = Guid.NewGuid();

        // The name of the database column that provides the Y-values for this series.
        // For a Pie Chart, this represents the column to be summed for a slice.
        public string ColumnName { get; set; }
    }
}