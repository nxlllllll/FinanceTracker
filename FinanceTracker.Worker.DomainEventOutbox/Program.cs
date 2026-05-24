using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.DomainEventOutbox.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.DomainEventOutbox;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqPublisher();

		DomainEventOutboxPublisherJobOptions jobOptions = builder.Configuration
			.GetSection(key: DomainEventOutboxPublisherJobOptions.SectionName)
			.Get<DomainEventOutboxPublisherJobOptions>() ?? new DomainEventOutboxPublisherJobOptions();

		builder.Services.AddOptions<DomainEventOutboxPublisherJobOptions>()
			.BindConfiguration(configSectionPath: DomainEventOutboxPublisherJobOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddQuartz(configure: q =>
		{
			q.AddJob<DomainEventOutboxPublisherJob>(configure: j => j.WithIdentity(name: nameof(DomainEventOutboxPublisherJob), group: jobOptions.Group));

			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(DomainEventOutboxPublisherJob), jobGroup: jobOptions.Group)
				.WithIdentity(name: jobOptions.TriggerName, group: jobOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInSeconds(seconds: jobOptions.IntervalSeconds).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.DomainEventOutbox");
		builder.Services.AddWorkerTracing(workerName: "Worker.DomainEventOutbox");

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}