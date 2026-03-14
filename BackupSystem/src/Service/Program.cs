using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using BackupSystem.Core;
using BackupSystem.Sources;
using BackupSystem.Destinations;
using BackupSystem.Scheduler;
using BackupSystem.Archiver;
using System.Text.Json;
using Serilog;
using Microsoft.Extensions.Options;

namespace BackupSystem.Service;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Initial Serilog setup
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("C:\\ProgramData\\BackupSystem\\Logs\\service.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        try
        {
            var host = CreateHostBuilder(args).Build();
            Log.Information("Backup System Service starting...");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Host terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
    
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("C:\\ProgramData\\BackupSystem\\Logs\\agent.log", rollingInterval: RollingInterval.Day))
            .ConfigureAppConfiguration((context, config) =>
            {
                var appDataPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "BackupSystem");
                
                config.SetBasePath(appDataPath);
                config.AddJsonFile("backup.json", optional: true, reloadOnChange: true);
                config.AddCommandLine(args);
            })
            .ConfigureServices((hostContext, services) =>
            {
                // Загрузка конфигурации
                var config = hostContext.Configuration;
                var backupConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "BackupSystem",
                    "backup.json");
                
                BackupConfiguration? backupConfig = null;
                
                if (File.Exists(backupConfigPath))
                {
                    try {
                        var json = File.ReadAllText(backupConfigPath);
                        backupConfig = JsonSerializer.Deserialize<BackupConfiguration>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    } catch (Exception ex) {
                        Log.Error(ex, "Error partial configuration from {Path}", backupConfigPath);
                    }
                }
                
                backupConfig ??= new BackupConfiguration();
                
                // Регистрация конфигурации через IOptions
                services.Configure<GlobalSettings>(hostContext.Configuration.GetSection("Global"));
                services.AddSingleton(backupConfig);
                services.AddSingleton(backupConfig.Global);
                services.AddSingleton(backupConfig.Jobs);
                
                // Регистрация архиватора
                services.AddSingleton<IArchiver, SharpZipArchiver>();
                
                // Регистрация источников и хранилищ через фабричные методы для DI
                services.AddSingleton<IEnumerable<IBackupSource>>(sp => 
                    CreateSources(sp.GetRequiredService<BackupConfiguration>(), sp.GetRequiredService<ILoggerFactory>()));
                
                services.AddSingleton<IEnumerable<IBackupDestination>>(sp => 
                    CreateDestinations(sp.GetRequiredService<BackupConfiguration>(), sp.GetRequiredService<ILoggerFactory>()));
                
                // Регистрация фабрик
                services.AddSingleton<IBackupSourceFactory, BackupSourceFactory>();
                services.AddSingleton<IBackupDestinationFactory, BackupDestinationFactory>();
                
                // Регистрация планировщика
                services.AddHostedService<BackupScheduler>();
            });
    
    private static List<IBackupSource> CreateSources(BackupConfiguration config, ILoggerFactory loggerFactory)
    {
        var sources = new List<IBackupSource>();
        
        foreach (var sourceConfig in config.Sources.Where(s => s.Enabled))
        {
            IBackupSource? source = sourceConfig.Type.ToLower() switch
            {
                "sqlserver" => new SqlServerSource(sourceConfig, loggerFactory.CreateLogger<SqlServerSource>()),
                "ones" => new OneSSource(sourceConfig, loggerFactory.CreateLogger<OneSSource>()),
                "files" => new FileSource(sourceConfig, loggerFactory.CreateLogger<FileSource>()),
                _ => null
            };
            
            if (source != null)
            {
                sources.Add(source);
            }
        }
        
        return sources;
    }
    
    private static List<IBackupDestination> CreateDestinations(BackupConfiguration config, ILoggerFactory loggerFactory)
    {
        var destinations = new List<IBackupDestination>();
        
        foreach (var destConfig in config.Destinations.Where(d => d.Enabled))
        {
            IBackupDestination? dest = destConfig.Type.ToLower() switch
            {
                "ftp" => new FtpDestination(destConfig, loggerFactory.CreateLogger<FtpDestination>()),
                "network" => new NetworkDestination(destConfig, loggerFactory.CreateLogger<NetworkDestination>()),
                _ => null
            };
            
            if (dest != null)
            {
                destinations.Add(dest);
            }
        }
        
        return destinations;
    }
}
