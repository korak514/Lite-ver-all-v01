// Views/CustomChartTooltip.xaml.cs
using System.Linq;
using System.ComponentModel;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;
using WPF_LoginForm.Models;

namespace WPF_LoginForm.Views
{
    public partial class CustomChartTooltip : UserControl, IChartTooltip
    {
        private TooltipData _data;

        public CustomChartTooltip()
        {
            InitializeComponent();
            // DataContext binds to itself so the ItemsControl can read "Data.Points"
            DataContext = this;
        }

        public TooltipData Data
        {
            get => _data;
            set
            {
                _data = value;
                OnPropertyChanged(nameof(Data));
                OnPropertyChanged(nameof(GlobalHeader));
                OnPropertyChanged(nameof(SharedTooltipLeft));
            }
        }

        public TooltipSelectionMode? SelectionMode { get; set; }

        public string GlobalHeader => Data?.Points?.FirstOrDefault()?.ChartPoint?.Instance is DashboardDataPoint dp ? dp.TooltipHeader : "";

        public string SharedTooltipLeft
        {
            get
            {
                if (Data?.Points == null || !Data.Points.Any()) return "";
                var first = (Data.Points.FirstOrDefault()?.ChartPoint?.Instance as DashboardDataPoint)?.TooltipLeft;
                if (Data.Points.All(p => (p.ChartPoint?.Instance as DashboardDataPoint)?.TooltipLeft == first))
                {
                    return first;
                }
                return null; // Not shared
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}