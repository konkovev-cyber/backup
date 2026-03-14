using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl;
using Quartz.Spi;
using BackupSystem.Core;

namespace BackupSystem.Scheduler;

/// <summary>
/// Планировщик задач на основе Quartz.NET
/// </summary>
public class BackupScheduler : IHostedService, IDisposable
{
    private readonly ILogger<BackupScheduler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<JobConfig> _jobConfigs;
    private IScheduler? _scheduler;
    
    public BackupScheduler(
        ILogger<BackupScheduler> logger,
        IServiceProvider serviceProvider,
        IEnumerable<JobConfig> jobConfigs)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _jobConfigs = jobConfigs;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting backup scheduler...");
        
        // Создание планировщика
        var factory = new StdSchedulerFactory();
        _scheduler = await factory.GetScheduler(cancellationToken);
        _scheduler.JobFactory = new BackupJobFactory(_serviceProvider);
        
        // Регистрация задач
        foreach (var config in _jobConfigs.Where(j => j.Enabled && j.Schedule != null))
        {
            await ScheduleJobAsync(config, cancellationToken);
        }
        
        await _scheduler.Start(cancellationToken);
        
        _logger.LogInformation("Backup scheduler started. {Count} jobs scheduled", 
            _jobConfigs.Count(j => j.Enabled && j.Schedule != null));
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping backup scheduler...");
        
        if (_scheduler != null)
        {
            await _scheduler.Shutdown(cancellationToken);
        }
        
        _logger.LogInformation("Backup scheduler stopped");
    }
    
    private async Task ScheduleJobAsync(JobConfig config, CancellationToken cancellationToken)
    {
        if (config.Schedule == null) return;
        
        var jobKey = new JobKey(config.Id, "backups");
        
        // Создание триггера на основе расписания
        var trigger = CreateTrigger(config.Schedule, config.Id);
        
        if (trigger != null)
        {
            await _scheduler!.ScheduleJob(trigger, cancellationToken);
            
            _logger.LogInformation("Scheduled job: {JobName} - {Schedule}", 
                config.Name, FormatSchedule(config.Schedule));
        }
    }
    
    private ITrigger? CreateTrigger(ScheduleConfig schedule, string jobId)
    {
        var triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(jobId, "backups")
            .WithDescription(schedule.Type);
        
        var timeStr = schedule.Time ?? "23:00";
        if (!TimeSpan.TryParse(timeStr, out var time))
        {
            time = new TimeSpan(23, 0, 0);
        }

        switch (schedule.Type.ToLower())
        {
            case "daily":
                return triggerBuilder
                    .WithDailySchedule(time.Hours, time.Minutes, time.Seconds)
                    .Build();
                
            case "weekly":
                var dayOfWeek = (DayOfWeek)(schedule.DayOfWeek ?? 0);
                return triggerBuilder
                    .WithWeeklySchedule(dayOfWeek, time.Hours, time.Minutes)
                    .Build();
                
            case "monthly":
                var dayOfMonth = schedule.DayOfMonth ?? 1;
                return triggerBuilder
                    .WithMonthlySchedule(dayOfMonth, time.Hours, time.Minutes)
                    .Build();
                
            case "interval":
                var intervalMinutes = schedule.IntervalMinutes ?? 60;
                return triggerBuilder
                    .StartNow()
                    .WithSimpleSchedule(x => x
                        .WithInterval(TimeSpan.FromMinutes(intervalMinutes))
                        .RepeatForever())
                    .Build();
                
            default:
                _logger.LogWarning("Unknown schedule type: {Type}", schedule.Type);
                return null;
        }
    }
    
    private string FormatSchedule(ScheduleConfig schedule)
    {
        return schedule.Type.ToLower() switch
        {
            "daily" => $"Daily at {schedule.Time}",
            "weekly" => $"Weekly on {(DayOfWeek)(schedule.DayOfWeek ?? 0)} at {schedule.Time}",
            "monthly" => $"Monthly on day {schedule.DayOfMonth} at {schedule.Time}",
            "interval" => $"Every {schedule.IntervalMinutes} minutes",
            _ => schedule.Type
        };
    }
    
    public void Dispose()
    {
        // Cleanup
    }
}

/// <summary>
/// Фабрика задач для Quartz.NET
/// </summary>
public class BackupJobFactory : IJobFactory
{
    private readonly IServiceProvider _serviceProvider;
    
    public BackupJobFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    {
        return new BackupExecutionJob(_serviceProvider);
    }
    
    public void ReturnJob(IJob job)
    {
        // Cleanup if needed
    }
}

/// <summary>
/// Задача выполнения бекапа
/// </summary>
public class BackupExecutionJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    
    public BackupExecutionJob(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.Trigger.Key.Name;
        var logger = _serviceProvider.GetService<ILogger<BackupExecutionJob>>();
        
        try
        {
            logger?.LogInformation("Job {JobId} triggered", jobId);
            
            // Resolve dependencies
            var jobConfigs = _serviceProvider.GetRequiredService<IEnumerable<JobConfig>>();
            var config = jobConfigs.FirstOrDefault(c => c.Id == jobId);
            
            if (config == null)
            {
                logger?.LogError("Job configuration not found for ID: {JobId}", jobId);
                return;
            }
            
            // Resolve other dependencies for BackupEngine
            var sourceFactory = _serviceProvider.GetRequiredService<IBackupSourceFactory>();
            var destFactory = _serviceProvider.GetRequiredService<IBackupDestinationFactory>();
            var globalSettings = _serviceProvider.GetRequiredService<IOptions<GlobalSettings>>().Value;
            var archiver = _serviceProvider.GetService<IArchiver>();
            var engineLogger = _serviceProvider.GetService<ILogger<BackupEngine>>();
            
            // Create sources and destinations
            var sources = config.SourceIds.Select(id => sourceFactory.Create(id)).Where(s => s != null).ToList();
            var destinations = config.DestinationIds.Select(id => destFactory.Create(id)).Where(d => d != null).ToList();
            
            if (!sources.Any())
            {
                logger?.LogWarning("No valid sources found for job: {JobName}", config.Name);
                return;
            }
            
            if (!destinations.Any())
            {
                logger?.LogWarning("No valid destinations found for job: {JobName}", config.Name);
                return;
            }

            // Execute engine
            var engine = new BackupEngine(config, sources!, destinations!, globalSettings, archiver, engineLogger);
            var result = await engine.RunAsync(context.CancellationToken);
            
            if (result.Success)
            {
                logger?.LogInformation("Job {JobName} completed successfully", config.Name);
            }
            else
            {
                logger?.LogError("Job {JobName} failed: {Error}", config.Name, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Unexpected error executing job {JobId}", jobId);
        }
    }
}

/// <summary>
/// Расширения для TriggerBuilder
/// </summary>
public static class TriggerBuilderExtensions
{
    public static TriggerBuilder WithDailySchedule(this TriggerBuilder builder, int hour, int minute, int second)
    {
        return builder.WithCronSchedule($"{second} {minute} {hour} * * ?");
    }
    
    public static TriggerBuilder WithWeeklySchedule(this TriggerBuilder builder, DayOfWeek dayOfWeek, int hour, int minute)
    {
        var dayChar = dayOfWeek switch
        {
            DayOfWeek.Sunday => "SUN",
            DayOfWeek.Monday => "MON",
            DayOfWeek.Tuesday => "TUE",
            DayOfWeek.Wednesday => "WED",
            DayOfWeek.Thursday => "THU",
            DayOfWeek.Friday => "FRI",
            DayOfWeek.Saturday => "SAT",
            _ => "MON"
        };
        
        return builder.WithCronSchedule($"0 {minute} {hour} ? * {dayChar}");
    }
    
    public static TriggerBuilder WithMonthlySchedule(this TriggerBuilder builder, int dayOfMonth, int hour, int minute)
    {
        return builder.WithCronSchedule($"0 {minute} {hour} {dayOfMonth} * ?");
    }
}
