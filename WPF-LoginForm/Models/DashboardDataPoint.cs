// Models/DashboardDataPoint.cs
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace WPF_LoginForm.Models
{
    public class DashboardDataPoint : INotifyPropertyChanged
    {
        public double X { get; set; }
        public double Y { get; set; }

        private string _label;
        public string Label
        {
            get => _label;
            set { var v = value?.Replace("_", " "); if (_label != v) { _label = v; OnPropertyChanged(); } }
        }

        // --- NEW PROPERTIES FOR CUSTOM 3-PART TOOLTIP ---
        private string _tooltipHeader;

        public string TooltipHeader
        {
            get => _tooltipHeader;
            set { var v = value?.Replace("_", " "); if (_tooltipHeader != v) { _tooltipHeader = v; OnPropertyChanged(); } }
        }

        private string _tooltipLeft;

        public string TooltipLeft
        {
            get => _tooltipLeft;
            set { var v = value?.Replace("_", " "); if (_tooltipLeft != v) { _tooltipLeft = v; OnPropertyChanged(); } }
        }

        private string _tooltipRight;

        public string TooltipRight
        {
            get => _tooltipRight;
            set { var v = value?.Replace("_", " "); if (_tooltipRight != v) { _tooltipRight = v; OnPropertyChanged(); } }
        }

        // ------------------------------------------------

        public bool IsImportant { get; set; }
        public bool HasLeaderLine { get; set; }
        public double LeaderLineX2 { get; set; }
        public double LeaderLineY2 { get; set; }

        private double _labelDx;

        public double LabelDx
        {
            get => _labelDx;
            set { if (_labelDx != value) { _labelDx = value; OnPropertyChanged(); } }
        }

        private double _labelDy;

        public double LabelDy
        {
            get => _labelDy;
            set { if (_labelDy != value) { _labelDy = value; OnPropertyChanged(); } }
        }

        private bool _showLabel;

        public bool ShowLabel
        {
            get => _showLabel;
            set { if (_showLabel != value) { _showLabel = value; OnPropertyChanged(); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}