using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Worker.DeadLetterMonitor.Jobs;
using Quartz;

namespace FinanceTracker.Worker.DeadLetterMonitor;

public sealed class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

        DeadLetterMonitoringOptions deadLetterOptions = builder.Configuration
            .GetSection(key: DeadLetterMonitoringOptions.SectionName)
            .Get<DeadLetterMonitoringOptions>() ?? new DeadLetterMonitoringOptions();

        builder.Services.AddQuartz(configure: q =>
        {
            q.AddJob<DeadLetterMonitoringJob>(configure: j =>
                j.WithIdentity(name: nameof(DeadLetterMonitoringJob), group: deadLetterOptions.Group));

            q.AddTrigger(configure: t => t
                .ForJob(jobName: nameof(DeadLetterMonitoringJob), jobGroup: deadLetterOptions.Group)
                .WithIdentity(name: deadLetterOptions.TriggerName, group: deadLetterOptions.Group)
                .WithSimpleSchedule(action: s => s
                    .WithIntervalInMinutes(minutes: deadLetterOptions.IntervalMinutes)
                    .RepeatForever()
                )
            );
        });

        builder.Services.AddQuartzHostedService(configure: options =>
            options.WaitForJobsToComplete = true);

        IHost app = builder.Build();
        app.Run();
    }
}