using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Worker.RecurringTransaction.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.RecurringTransaction;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services
			.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqPublisher();

		RecurringTransactionJobOptions recurringOptions = builder.Configuration
			.GetSection(key: RecurringTransactionJobOptions.SectionName)
			.Get<RecurringTransactionJobOptions>() ?? new RecurringTransactionJobOptions();

		builder.Services.AddQuartz(configure: q =>
		{
			q.AddJob<RecurringTransactionHandlingJob>(configure: j =>
				j.WithIdentity(name: nameof(RecurringTransactionHandlingJob), group: recurringOptions.Group)
			);
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(RecurringTransactionHandlingJob), jobGroup: recurringOptions.Group)
				.WithIdentity(name: recurringOptions.TriggerName, group: recurringOptions.Group)
				.WithCronSchedule(
					cronExpression: recurringOptions.CronExpression,
					schedule => schedule
						.InTimeZone(tz: TimeZoneInfo.Utc)
						.WithMisfireHandlingInstructionFireAndProceed()
				)
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(
			name: "FinanceTrackerContext"
		)!;

		builder.Services
			.AddWorkerHealthChecks(connectionString: connectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.RecurringTransaction");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}