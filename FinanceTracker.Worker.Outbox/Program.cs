using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Outbox.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.Outbox;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqPublisher();

		OutboxOptions outboxOptions = builder.Configuration
			.GetSection(key: OutboxOptions.SectionName)
			.Get<OutboxOptions>() ?? new OutboxOptions();

		builder.Services.AddOptions<OutboxOptions>()
			.BindConfiguration(configSectionPath: OutboxOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddQuartz(configure: q =>
		{
			q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "OutboxScheduler");

			q.AddJob<OutboxPublisherJob>(configure: j => j.WithIdentity(name: nameof(OutboxPublisherJob), group: outboxOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(OutboxPublisherJob), jobGroup: outboxOptions.Group)
				.WithIdentity(name: outboxOptions.TriggerName, group: outboxOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInSeconds(seconds: outboxOptions.IntervalSeconds).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.Outbox");
		builder.Services.AddWorkerTracing(workerName: "Worker.Outbox");
		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}
