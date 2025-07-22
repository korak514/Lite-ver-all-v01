using System.Collections.Generic;

namespace WPF_LoginForm.Models
{
    public class DashboardConfiguration
    {
        public ChartConfiguration BarChart { get; set; }
        public ChartConfiguration LineChart { get; set; }
        public ChartConfiguration CustomGraph { get; set; }
        public ChartConfiguration PieChart { get; set; }
        public ChartConfiguration SmallChart { get; set; }
    }

    public class ChartConfiguration
    {
        public string Title { get; set; }
        public string DataSource { get; set; }
        public string XAxis { get; set; }
        public string YAxis { get; set; }
        public string ChartType { get; set; }
    }
}
