using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.DeadLetterMonitor.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.DeadLetterMonitor;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

        builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddOptions<DeadLetterMonitoringOptions>()
			.BindConfiguration(configSectionPath: DeadLetterMonitoringOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		
        DeadLetterMonitoringOptions deadLetterOptions = builder.Configuration
            .GetSection(key: DeadLetterMonitoringOptions.SectionName)
            .Get<DeadLetterMonitoringOptions>() ?? new DeadLetterMonitoringOptions();

        builder.Services.AddQuartz(configure: q =>
        {
            q.AddJob<DeadLetterMonitoringJob>(configure: j => j.WithIdentity(name: nameof(DeadLetterMonitoringJob), group: deadLetterOptions.Group));

            q.AddTrigger(configure: t => t
                .ForJob(jobName: nameof(DeadLetterMonitoringJob), jobGroup: deadLetterOptions.Group)
                .WithIdentity(name: deadLetterOptions.TriggerName, group: deadLetterOptions.Group)
                .WithSimpleSchedule(action: s => s.WithIntervalInMinutes(minutes: deadLetterOptions.IntervalMinutes).RepeatForever())
            );
        });

		builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.DeadLetterMonitor");
		builder.Services.AddWorkerTracing(workerName: "Worker.DeadLetterMonitor");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}
