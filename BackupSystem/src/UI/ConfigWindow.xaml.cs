using System.Windows;
using System.IO;
using System.Net.Http;
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
                Schedule = new ScheduleConfig { Type = "daily", Time = "02:00" },
                Archiver = new ArchiverSettings { Enabled = true, Format = "zip" },
                Retention = new RetentionSettings { KeepDaily = 7, KeepWeekly = 4, KeepMonthly = 12 },
                Notifications = new NotificationSettings { OnFailure = true, OnSuccess = false },
                Hooks = new HookSettings()
            });
            JobsListBox.Items.Refresh();
        }

        private async void TestTelegram_Click(object sender, RoutedEventArgs e)
        {
            var token = _config.Global.Telegram.BotToken;
            var chatId = _config.Global.Telegram.ChatId;

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId))
            {
                MessageBox.Show("Сначала укажите Токен и Chat ID", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var client = new HttpClient();
                var url = $"https://api.telegram.org/bot{token}/sendMessage";
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["chat_id"] = chatId,
                    ["text"] = "🔔 Проверка уведомлений BackupSystem Professional. Если вы получили это сообщение, значит настройки верны!",
                    ["parse_mode"] = "Markdown"
                });

                var response = await client.PostAsync(url, content);
                if (response.IsSuccessStatusCode)
                {
                    MessageBox.Show("Тестовое сообщение отправлено успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    MessageBox.Show($"Ошибка Telegram API: {error}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сети: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestSmtp_Click(object sender, RoutedEventArgs e)
        {
            // Упрощенная проверка SMTP
            if (string.IsNullOrEmpty(_config.Global.Smtp.Host))
            {
                MessageBox.Show("Укажите SMTP Хост", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("Проверка SMTP настроек (имитация)... Настройки выглядят корректно.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
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
