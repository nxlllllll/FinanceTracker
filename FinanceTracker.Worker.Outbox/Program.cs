using FinanceTracker.Worker.Outbox.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.Outbox;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqPublisher()
			.AddRabbitMqHealthCheck();

		OutboxOptions outboxOptions = builder.AddValidatedOptions<OutboxOptions>(sectionName: OutboxOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddIntervalJob<OutboxPublisherJob>(
			group: outboxOptions.Group,
			triggerName: outboxOptions.TriggerName,
			interval: TimeSpan.FromSeconds(value: outboxOptions.IntervalSeconds)
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
