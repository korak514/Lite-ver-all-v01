using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Newtonsoft.Json;
using WPF_LoginForm.Models;
using WPF_LoginForm.Views;
using WPF_LoginForm.Properties; // Required for Settings.Default

namespace WPF_LoginForm.ViewModels
{
    public class DashboardPortalViewModel : ViewModelBase
    {
        private readonly string _configPath;
        private PortalConfiguration _config;

        public ObservableCollection<PortalButtonViewModel> PortalButtons { get; }

        // NEW: Collection for the ComboBox in Settings
        public ObservableCollection<string> AvailableDashboardFiles { get; }

        // Action to trigger when a button is clicked (passes the JSON filename to the MainViewModel)
        public Action<string> OpenDashboardAction { get; set; }

        public ICommand OpenPortalSettingsCommand { get; }
        public ICommand SavePortalSettingsCommand { get; }

        public DashboardPortalViewModel()
        {
            _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WPF_LoginForm", "portal_config.json");
            PortalButtons = new ObservableCollection<PortalButtonViewModel>();
            AvailableDashboardFiles = new ObservableCollection<string>();

            OpenPortalSettingsCommand = new ViewModelCommand(ExecuteOpenSettings);
            SavePortalSettingsCommand = new ViewModelCommand(ExecuteSaveSettings);

            LoadAvailableFiles();
            LoadConfig();
        }

        // NEW: Scans the target folder for JSON files, identical to HomeViewModel logic
        private void LoadAvailableFiles()
        {
            AvailableDashboardFiles.Clear();
            AvailableDashboardFiles.Add(""); // Add an empty option so users can clear a slot

            string path = Settings.Default.ImportIsRelative ? AppDomain.CurrentDomain.BaseDirectory : Settings.Default.ImportAbsolutePath;

            if (Directory.Exists(path))
            {
                try
                {
                    foreach (var f in Directory.GetFiles(path, "*.json"))
                    {
                        string fileName = Path.GetFileName(f);
                        // Prevent users from accidentally selecting internal config files
                        if (!fileName.Equals("portal_config.json", StringComparison.OrdinalIgnoreCase) &&
                            !fileName.Equals("category_rules.json", StringComparison.OrdinalIgnoreCase))
                        {
                            AvailableDashboardFiles.Add(fileName);
                        }
                    }
                }
                catch { }
            }
        }

        private void LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try { _config = JsonConvert.DeserializeObject<PortalConfiguration>(File.ReadAllText(_configPath)); }
                catch { _config = new PortalConfiguration(); }
            }
            else { _config = new PortalConfiguration(); }

            // Ensure exactly 6 buttons exist
            for (int i = 1; i <= 6; i++)
            {
                if (!_config.Buttons.Any(b => b.Id == i))
                {
                    _config.Buttons.Add(new PortalButtonConfig { Id = i, Title = $"Dashboard Module {i}", Description = "Unassigned", DashboardFileName = "" });
                }
            }

            PortalButtons.Clear();
            foreach (var b in _config.Buttons.OrderBy(x => x.Id))
            {
                var vm = new PortalButtonViewModel(b);
                vm.ClickCommand = new ViewModelCommand(p =>
                {
                    if (vm.IsConfigured) OpenDashboardAction?.Invoke(vm.DashboardFileName);
                    else ExecuteOpenSettings(null); // Open settings if file isn't linked
                });
                PortalButtons.Add(vm);
            }
        }

        private void ExecuteOpenSettings(object obj)
        {
            // Refresh files right before opening settings in case user added a new json file
            LoadAvailableFiles();

            // Open the configuration window and bind it to this same ViewModel
            var settingsWindow = new PortalSettingsWindow
            {
                DataContext = this,
                Owner = System.Windows.Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
        }

        private void ExecuteSaveSettings(object obj)
        {
            // Update the underlying models from the ViewModels
            _config.Buttons = PortalButtons.Select(pb => pb.GetModel()).ToList();

            // Save to JSON
            try
            {
                string dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_configPath, JsonConvert.SerializeObject(_config, Formatting.Indented));

                // Force UI update
                foreach (var pb in PortalButtons) pb.Refresh();

                System.Windows.MessageBox.Show("Portal configuration saved successfully!", "Saved", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving config: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }

            // Close the window automatically if the Command Parameter passed the Window reference
            if (obj is System.Windows.Window win)
            {
                win.Close();
            }
        }
    }

    public class PortalButtonViewModel : ViewModelBase
    {
        private PortalButtonConfig _model;
        public ICommand ClickCommand { get; set; }

        public int Id => _model.Id;
        public string Title
        { get => _model.Title; set { _model.Title = value; OnPropertyChanged(); } }
        public string Description
        { get => _model.Description; set { _model.Description = value; OnPropertyChanged(); } }
        public string DashboardFileName
        { get => _model.DashboardFileName; set { _model.DashboardFileName = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsConfigured)); } }

        public bool IsConfigured => !string.IsNullOrEmpty(_model.DashboardFileName);

        public PortalButtonViewModel(PortalButtonConfig model)
        { _model = model; }

        public PortalButtonConfig GetModel() => _model;

        public void Refresh()
        { OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Description)); OnPropertyChanged(nameof(IsConfigured)); }
    }
}