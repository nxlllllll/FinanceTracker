using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Outbox.Jobs;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.Outbox;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services
			.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqPublisher();

		OutboxOptions outboxOptions = builder.Configuration
			.GetSection(key: OutboxOptions.SectionName)
			.Get<OutboxOptions>() ?? new OutboxOptions();

		builder.Services.AddOptions<OutboxOptions>()
			.BindConfiguration(configSectionPath: OutboxOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddQuartz(configure: q =>
		{
			q.AddJob<OutboxPublisherJob>(configure: j => j.WithIdentity(name: nameof(OutboxPublisherJob), group: outboxOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(OutboxPublisherJob), jobGroup: outboxOptions.Group)
				.WithIdentity(name: outboxOptions.TriggerName, group: outboxOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInSeconds(seconds: outboxOptions.IntervalSeconds).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: o => o.WaitForJobsToComplete = true);

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services
			.AddWorkerHealthChecks(connectionString: connectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.Outbox");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}