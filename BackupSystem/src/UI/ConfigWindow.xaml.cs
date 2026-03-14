using System.Windows;
using System.IO;
using BackupSystem.Core;

namespace BackupSystem.UI
{
    public partial class ConfigWindow : Window
    {
        private BackupConfiguration _config;
        private string _configPath;

        public ConfigWindow()
        {
            InitializeComponent();
            _configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "BackupSystem", "backup.json");
            
            _config = LoadConfiguration();
            DataContext = _config;
            
            SourcesListBox.ItemsSource = _config.Sources;
            DestinationsListBox.ItemsSource = _config.Destinations;
            JobsListBox.ItemsSource = _config.Jobs;
            
            LoadGlobalSettings();
        }
        
        private BackupConfiguration LoadConfiguration()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return System.Text.Json.JsonSerializer.Deserialize<BackupConfiguration>(json, options) ?? new BackupConfiguration();
                }
                catch { }
            }
            return new BackupConfiguration();
        }

        private void LoadGlobalSettings()
        {
            TempPathTextBox.Text = _config.Global.TempPath;
            LogLevelComboBox.Text = _config.Global.LogLevel;
            MaxJobsTextBox.Text = _config.Global.MaxConcurrentJobs.ToString();
            TimeoutTextBox.Text = _config.Global.OperationTimeoutMinutes.ToString();
            RetryCountTextBox.Text = _config.Global.RetryCount.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update global settings from UI
            _config.Global.TempPath = TempPathTextBox.Text;
            _config.Global.LogLevel = LogLevelComboBox.Text;
            _config.Global.MaxConcurrentJobs = int.TryParse(MaxJobsTextBox.Text, out var mj) ? mj : 1;
            _config.Global.OperationTimeoutMinutes = int.TryParse(TimeoutTextBox.Text, out var to) ? to : 60;
            _config.Global.RetryCount = int.TryParse(RetryCountTextBox.Text, out var rc) ? rc : 3;

            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                
                var json = System.Text.Json.JsonSerializer.Serialize(_config, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configPath, json);
                
                MessageBox.Show("Настройки успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
        
        private void AddSource_Click(object sender, RoutedEventArgs e)
        {
            _config.Sources.Add(new SourceConfig 
            { 
                Id = Guid.NewGuid().ToString().Substring(0, 8), 
                Name = "Новый источник", 
                Type = "File",
                Settings = new Dictionary<string, object> { ["paths"] = @"C:\BackupSource" }
            });
            SourcesListBox.Items.Refresh();
        }
        
        private void AddDestination_Click(object sender, RoutedEventArgs e)
        {
            _config.Destinations.Add(new DestinationConfig 
            { 
                Id = Guid.NewGuid().ToString().Substring(0, 8), 
                Name = "Новое хранилище", 
                Type = "Local",
                Settings = new Dictionary<string, object> { ["path"] = @"D:\Backups" }
            });
            DestinationsListBox.Items.Refresh();
        }
        
        private void AddJob_Click(object sender, RoutedEventArgs e)
        {
            _config.Jobs.Add(new JobConfig 
            { 
                Id = Guid.NewGuid().ToString().Substring(0, 8), 
                Name = "Новая задача",
                Archiver = new ArchiverSettings(),
                Retention = new RetentionSettings { KeepDaily = 3, KeepWeekly = 1, KeepMonthly = 0 }
            });
            JobsListBox.Items.Refresh();
        }
    }

    public class ConfigValueConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Dictionary<string, object> dict && parameter is string key)
            {
                if (dict.TryGetValue(key, out var val))
                {
                    if (val is System.Text.Json.JsonElement element) return element.ToString();
                    return val?.ToString() ?? "";
                }
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // This is tricky for two-way binding. We'll handle it if needed.
            return value;
        }
    }

    public class EqualsConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
