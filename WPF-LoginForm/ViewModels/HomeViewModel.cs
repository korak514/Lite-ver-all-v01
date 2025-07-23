using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Input;
using LiveCharts;
using LiveCharts.Wpf;
using WPF_LoginForm.Models;
using WPF_LoginForm.Repositories;
using WPF_LoginForm.Services;
using WPF_LoginForm.Views;

namespace WPF_LoginForm.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        public ICommand ConfigureCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }

        private DateTime _startDate = new DateTime(2023, 1, 1);
        private DateTime _endDate;

        private double _sliderMin;
        public double SliderMin
        {
            get => _sliderMin;
            set
            {
                _sliderMin = value;
                OnPropertyChanged(nameof(SliderMin));
            }
        }

        private double _sliderMax;
        public double SliderMax
        {
            get => _sliderMax;
            set
            {
                _sliderMax = value;
                OnPropertyChanged(nameof(SliderMax));
            }
        }

        private double _sliderValue;
        public double SliderValue
        {
            get => _sliderValue;
            set
            {
                _sliderValue = value;
                OnPropertyChanged(nameof(SliderValue));
                UpdateSelectedDateRangeText();
            }
        }

        private string _selectedDateRangeText;
        public string SelectedDateRangeText
        {
            get => _selectedDateRangeText;
            set
            {
                _selectedDateRangeText = value;
                OnPropertyChanged(nameof(SelectedDateRangeText));
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
            _dataRepository = new DataRepository(new FileLogger());
            ConfigureCommand = new ViewModelCommand(p => ShowConfigurationWindow());
            ImportCommand = new ViewModelCommand(p => ImportConfiguration());
            ExportCommand = new ViewModelCommand(p => ExportConfiguration());

            _endDate = DateTime.Now;
            SliderMin = 0;
            SliderMax = (_endDate - _startDate).TotalDays;
            SliderValue = SliderMax;
            UpdateSelectedDateRangeText();

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

        private void UpdateSelectedDateRangeText()
        {
            DateTime selectedEndDate = _startDate.AddDays(_sliderValue);
            SelectedDateRangeText = $"Jan 1, 2023 – {selectedEndDate:MMM d, yyyy}";
            LoadData();
        }

        private readonly IDataRepository _dataRepository;

        private async void LoadData()
        {
            if (_dashboardConfiguration == null) return;

            var startDate = _startDate;
            var endDate = _startDate.AddDays(_sliderValue);

            var dataTable = await _dataRepository.GetDataAsync(_dashboardConfiguration.TableName, _dashboardConfiguration.XAxisColumn, _dashboardConfiguration.YAxisColumns, startDate, endDate);

            if (dataTable == null) return;

            var labels = dataTable.AsEnumerable().Select(r => r[_dashboardConfiguration.XAxisColumn].ToString()).ToArray();
            var series = new SeriesCollection();

            foreach (var columnName in _dashboardConfiguration.YAxisColumns)
            {
                var values = new ChartValues<double>();
                foreach (DataRow row in dataTable.Rows)
                {
                    values.Add(Convert.ToDouble(row[columnName]));
                }

                series.Add(new ColumnSeries
                {
                    Title = columnName,
                    Values = values
                });
            }

            BarChartSeries = series;
            BarChartLabels = labels;
        }

        private DashboardConfiguration _dashboardConfiguration;

        private void ShowConfigurationWindow()
        {
            var configView = new ConfigurationView();
            var configViewModel = new ConfigurationViewModel();
            configView.DataContext = configViewModel;
            if (configView.ShowDialog() == true)
            {
                _dashboardConfiguration = new DashboardConfiguration
                {
                    TableName = configViewModel.SelectedTable,
                    XAxisColumn = configViewModel.SelectedXAxisColumn,
                    YAxisColumns = configViewModel.SelectableColumns.Where(c => c.IsSelected).Select(c => c.Name).ToList(),
                    ChartType = configViewModel.SelectedChartType
                };
                LoadData();
            }
        }
    }
}
