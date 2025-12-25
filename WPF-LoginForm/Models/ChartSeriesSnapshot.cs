using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class ChartSeriesSnapshot
    {
        public int ChartPosition { get; set; }
        public string SeriesTitle { get; set; }
        public string HexColor { get; set; }
        public List<object> DataPoints { get; set; } = new List<object>();
    }
}