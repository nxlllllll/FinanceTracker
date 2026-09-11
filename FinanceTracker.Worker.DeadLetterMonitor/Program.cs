using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.DeadLetterMonitor.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.DeadLetterMonitor;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.AddStructuredLogging();

		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddOptions<UnresolvableEventGaugeOptions>()
			.BindConfiguration(configSectionPath: UnresolvableEventGaugeOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddOptions<DeadLetterBacklogSummaryOptions>()
			.BindConfiguration(configSectionPath: DeadLetterBacklogSummaryOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		UnresolvableEventGaugeOptions gaugeOptions = builder.Configuration
			.GetSection(key: UnresolvableEventGaugeOptions.SectionName)
			.Get<UnresolvableEventGaugeOptions>() ?? new UnresolvableEventGaugeOptions();

		DeadLetterBacklogSummaryOptions backlogSummaryOptions = builder.Configuration
			.GetSection(key: DeadLetterBacklogSummaryOptions.SectionName)
			.Get<DeadLetterBacklogSummaryOptions>() ?? new DeadLetterBacklogSummaryOptions();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddQuartz(configure: q =>
		{
			q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "DeadLetterMonitorScheduler");

			q.AddJob<UnresolvableEventGaugeJob>(configure: j => j.WithIdentity(name: nameof(UnresolvableEventGaugeJob), group: gaugeOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(UnresolvableEventGaugeJob), jobGroup: gaugeOptions.Group)
				.WithIdentity(name: gaugeOptions.TriggerName, group: gaugeOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInMinutes(minutes: gaugeOptions.IntervalMinutes).RepeatForever())
			);

			q.AddJob<DeadLetterBacklogSummaryJob>(configure: j => j.WithIdentity(name: nameof(DeadLetterBacklogSummaryJob), group: backlogSummaryOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(DeadLetterBacklogSummaryJob), jobGroup: backlogSummaryOptions.Group)
				.WithIdentity(name: backlogSummaryOptions.TriggerName, group: backlogSummaryOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInMinutes(minutes: backlogSummaryOptions.IntervalMinutes).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);

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
