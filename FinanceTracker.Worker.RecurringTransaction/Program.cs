using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.RecurringTransaction.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.RecurringTransaction;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqPublisher();

		builder.Services.AddOptions<RecurringTransactionJobOptions>()
			.BindConfiguration(configSectionPath: RecurringTransactionJobOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		RecurringTransactionJobOptions recurringOptions = builder.Configuration
			.GetSection(key: RecurringTransactionJobOptions.SectionName)
			.Get<RecurringTransactionJobOptions>() ?? new RecurringTransactionJobOptions();

		builder.Services.AddQuartz(configure: q =>
		{
			q.AddJob<RecurringTransactionHandlingJob>(configure: j => j.WithIdentity(name: nameof(RecurringTransactionHandlingJob), group: recurringOptions.Group));

			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(RecurringTransactionHandlingJob), jobGroup: recurringOptions.Group)
				.WithIdentity(name: recurringOptions.TriggerName, group: recurringOptions.Group)
				.WithCronSchedule(
					cronExpression: recurringOptions.CronExpression,
					schedule => schedule.InTimeZone(tz: TimeZoneInfo.Utc).WithMisfireHandlingInstructionFireAndProceed()
				)
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.RecurringTransaction");
		builder.Services.AddWorkerTracing(workerName: "Worker.RecurringTransaction");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}