using System;
using System.Collections.Generic;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using WPF_LoginForm.Models;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        public ICommand ConfigureCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }

        private DateTime? _selectedDate;
        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                _selectedDate = value;
                OnPropertyChanged(nameof(SelectedDate));
            }
        }

        public SeriesCollection BarChartSeries { get; set; }
        public string[] BarChartLabels { get; set; }
        public SeriesCollection LineChartSeries { get; set; }
        public string[] LineChartLabels { get; set; }
        public SeriesCollection CustomGraphSeries { get; set; }
        public string[] CustomGraphLabels { get; set; }
        public SeriesCollection PieChartSeries { get; set; }
        public SeriesCollection SmallChartSeries { get; set; }
        public string[] SmallChartLabels { get; set; }

        public HomeViewModel()
        {
            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());
            SelectedDate = DateTime.Now;

            // Initialize chart data
            BarChartSeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Sales",
                    Values = new ChartValues<double> { 10, 20, 30, 40, 50 }
                }
            };
            BarChartLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };

            LineChartSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Revenue",
                    Values = new ChartValues<double> { 100, 120, 110, 130, 150 }
                }
            };
            LineChartLabels = new[] { "Jan", "Feb", "Mar", "Apr", "May" };

            PieChartSeries = new SeriesCollection
            {
                new PieSeries
                {
                    Title = "Product A",
                    Values = new ChartValues<double> { 30 }
                },
                new PieSeries
                {
                    Title = "Product B",
                    Values = new ChartValues<double> { 40 }
                },
                new PieSeries
                {
                    Title = "Product C",
                    Values = new ChartValues<double> { 30 }
                }
            };
        }

        private void ImportConfiguration()
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                var config = Newtonsoft.Json.JsonConvert.DeserializeObject<DashboardConfiguration>(json);
                // Apply the loaded configuration to the charts
            }
        }

        private void ExportConfiguration()
        {
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*"
            };
            if (saveFileDialog.ShowDialog() == true)
            {
                var config = new DashboardConfiguration
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
