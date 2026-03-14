using System.Windows;
using System.Windows.Controls;
using System.IO;
using BackupSystem.Core;

namespace BackupSystem.UI
{
    public partial class MainWindow : Window
    {
        private BackupConfiguration? _config;
        private List<JobConfig> _jobs = new();
        
        public MainWindow()
        {
            InitializeComponent();
            LoadConfiguration();
            UpdateUI();
        }
        
        private void LoadConfiguration()
        {
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "BackupSystem",
                "backup.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    _config = System.Text.Json.JsonSerializer.Deserialize<BackupConfiguration>(json, options);
                }
                catch { }
            }
            
            if (_config == null)
            {
                _config = new BackupConfiguration();
                // Демо данные
                _config.Jobs = new List<JobConfig>
                {
                    new JobConfig { Id = "job-1", Name = "Ежедневный бекап", Enabled = true },
                    new JobConfig { Id = "job-2", Name = "Еженедельный архив", Enabled = true }
                };
            }
            
            _jobs = _config.Jobs;
            JobsListBox.ItemsSource = _jobs;
            HistoryDataGrid.ItemsSource = _config.History.OrderByDescending(h => h.Timestamp).ToList();
        }
        
        private void UpdateUI()
        {
            if (_jobs.Count > 0)
            {
                JobsListBox.SelectedIndex = 0;
            }
        }
        
        private async void RunBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (JobsListBox.SelectedItem is not JobConfig job) return;

            RunBackupButton.IsEnabled = false;
            StatusText.Text = "Выполнение бекапа...";
            
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Запуск задачи: {job.Name}...\n");
            
            // Имитация процесса
            for (int i = 0; i <= 100; i += 10)
            {
                Progress_bar.Value = i;
                ProgressText.Text = $"{i}%";
                await Task.Delay(200);
            }
            
            // Добавим в историю
            var historyItem = new BackupHistory
            {
                JobId = job.Id,
                JobName = job.Name,
                Timestamp = DateTime.Now,
                Success = true,
                Message = "Выполнено успешно (демо)",
                SizeBytes = 1024 * 1024 * 5 // 5 MB
            };
            
            _config?.History.Add(historyItem);
            HistoryDataGrid.ItemsSource = _config?.History.OrderByDescending(h => h.Timestamp).ToList();
            
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Бекап завершён успешно\n");
            
            RunBackupButton.IsEnabled = true;
            StatusText.Text = "Готов к работе";
            LastRunText.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
        }
        
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var configWindow = new ConfigWindow();
            if (configWindow.ShowDialog() == true)
            {
                LoadConfiguration();
                UpdateUI();
            }
        }
        
        private void ServiceButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Управление службой будет реализовано", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void JobsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (JobsListBox.SelectedItem is JobConfig job)
            {
                JobNameText.Text = job.Name;
                JobDescriptionText.Text = $"ID: {job.Id}\nИсточников: {job.SourceIds.Count}, Хранилищ: {job.DestinationIds.Count}";
            }
        }

        private void RestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BackupHistory history)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите восстановить данные от {history.Timestamp}?\n\nВнимание: Это может перезаписать текущие файлы!", 
                    "Восстановление", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    MessageBox.Show("Процесс восстановления запущен. Следите за логами.", "Запуск", MessageBoxButton.OK, MessageBoxImage.Information);
                    LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Запуск восстановления из {history.Timestamp}...\n");
                    // TODO: Реализовать восстановление
                }
            }
        }
    }

    public class BooleanToStatusConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? "Успешно" : "Ошибка";
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }

    public class SizeConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            long bytes = (long)value;
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0" + suf[0];
            long place = Convert.ToInt64(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return num.ToString() + " " + suf[place];
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
    }
}
