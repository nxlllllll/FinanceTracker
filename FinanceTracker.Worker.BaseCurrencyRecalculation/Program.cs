using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.BaseCurrencyRecalculation.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.BaseCurrencyRecalculation;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.AddStructuredLogging();

		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddOptions<BaseCurrencyRecalculationJobOptions>()
			.BindConfiguration(configSectionPath: BaseCurrencyRecalculationJobOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		BaseCurrencyRecalculationJobOptions jobOptions = builder.Configuration
			.GetSection(key: BaseCurrencyRecalculationJobOptions.SectionName)
			.Get<BaseCurrencyRecalculationJobOptions>() ?? new BaseCurrencyRecalculationJobOptions();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddQuartz(configure: q =>
		{
			q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "BaseCurrencyRecalculationScheduler");

			q.AddJob<BaseCurrencyRecalculationJob>(configure: j => j.WithIdentity(name: nameof(BaseCurrencyRecalculationJob), group: jobOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(BaseCurrencyRecalculationJob), jobGroup: jobOptions.Group)
				.WithIdentity(name: jobOptions.TriggerName, group: jobOptions.Group)
				.WithCronSchedule(
					cronExpression: jobOptions.CronExpression,
					schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
				)
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.BaseCurrencyRecalculation");
		builder.Services.AddWorkerTracing(workerName: "Worker.BaseCurrencyRecalculation");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}
