using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Cleanup.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.Cleanup;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddOptions<CleanupOptions>()
			.BindConfiguration(configSectionPath: CleanupOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		CleanupOptions cleanupOptions = builder.Configuration
			.GetSection(key: CleanupOptions.SectionName)
			.Get<CleanupOptions>() ?? new CleanupOptions();

		builder.Services.AddQuartz(configure: q =>
		{
			q.AddJob<CleanupJob>(configure: j => j.WithIdentity(name: nameof(CleanupJob), group: cleanupOptions.Group));

			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(CleanupJob), jobGroup: cleanupOptions.Group)
				.WithIdentity(name: cleanupOptions.TriggerName, group: cleanupOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInMinutes(minutes: cleanupOptions.IntervalMinutes).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.Cleanup");
		builder.Services.AddWorkerTracing(workerName: "Worker.Cleanup");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}