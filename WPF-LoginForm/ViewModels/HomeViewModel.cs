using System.Windows.Input;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        public ICommand ConfigureCommand { get; }

        public HomeViewModel()
        {
            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
        }

        public LiveCharts.SeriesCollection BarChartSeries { get; set; }
        public string[] BarChartLabels { get; set; }
        public LiveCharts.SeriesCollection LineChartSeries { get; set; }
        public string[] LineChartLabels { get; set; }
        public LiveCharts.SeriesCollection CustomGraphSeries { get; set; }
        public string[] CustomGraphLabels { get; set; }
        public LiveCharts.SeriesCollection PieChartSeries { get; set; }
        public LiveCharts.SeriesCollection SmallChartSeries { get; set; }
        public string[] SmallChartLabels { get; set; }

        private System.DateTime? _selectedDate;
        public System.DateTime? SelectedDate
        {
            get { return _selectedDate; }
            set
            {
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
                // Filter chart data based on the selected date
            }
        }

        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }

        public HomeViewModel()
        {
            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            SelectedDate = System.DateTime.Now;

            // Initialize chart data
            BarChartSeries = new LiveCharts.SeriesCollection
            {
                new LiveCharts.Wpf.ColumnSeries
                {
                    Title = "Sales",
                    Values = new LiveCharts.ChartValues<double> { 10, 20, 30, 40, 50 }
                }
            };
            BarChartLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };

            LineChartSeries = new LiveCharts.SeriesCollection
            {
                new LiveCharts.Wpf.LineSeries
                {
                    Title = "Revenue",
                    Values = new LiveCharts.ChartValues<double> { 100, 120, 110, 130, 150 }
                }
            };
            LineChartLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };

            PieChartSeries = new LiveCharts.SeriesCollection
            {
                new LiveCharts.Wpf.PieSeries
                {
                    Title = "Product A",
                    Values = new LiveCharts.ChartValues<double> { 30 }
                },
                new LiveCharts.Wpf.PieSeries
                {
                    Title = "Product B",
                    Values = new LiveCharts.ChartValues<double> { 40 }
                },
                new LiveCharts.Wpf.PieSeries
                {
                    Title = "Product C",
                    Values = new LiveCharts.ChartValues<double> { 30 }
                }
            };
        }

        private void ImportConfiguration()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog();
            openFileDialog.Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.DashboardConfiguration>(json);
                // Apply the loaded configuration to the charts
            }
        }

        private void ExportConfiguration()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*";
            if (saveFileDialog.ShowDialog() == true)
            {
                var config = new Models.DashboardConfiguration
                {
                    // Populate the config object with the current chart settings
                };
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(config, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText(saveFileDialog.FileName, json);
            }
        }

        private void ShowConfigurationWindow()
        {
            var configView = new ConfigurationView();
            var configViewModel = new ConfigurationViewModel();
            configView.DataContext = configViewModel;
            if (configView.ShowDialog() == true)
            {
                // Apply the configuration
            }
        }
    }
}
