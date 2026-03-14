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
                var json = File.ReadAllText(configPath);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _config = System.Text.Json.JsonSerializer.Deserialize<BackupConfiguration>(json, options);
                _jobs = _config?.Jobs ?? new List<JobConfig>();
            }
            else
            {
                // Демо данные
                _jobs = new List<JobConfig>
                {
                    new JobConfig 
                    { 
                        Id = "job-1", 
                        Name = "Ежедневный бекап",
                        Enabled = true
                    },
                    new JobConfig 
                    { 
                        Id = "job-2", 
                        Name = "Еженедельный архив",
                        Enabled = true
                    }
                };
            }
            
            JobsListBox.ItemsSource = _jobs;
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
            RunBackupButton.IsEnabled = false;
            StatusText.Text = "Выполнение бекапа...";
            
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] Запуск бекапа...\n");
            
            // Имитация процесса
            for (int i = 0; i <= 100; i += 10)
            {
                Progress_bar.Value = i;
                ProgressText.Text = $"{i}%";
                await Task.Delay(200);
            }
            
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
            // TODO: Управление службой
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
    }
}
