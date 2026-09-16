using FinanceTracker.Worker.RecurringTransaction.Job;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.RecurringTransaction;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqPublisher()
			.AddRabbitMqHealthCheck();

		RecurringTransactionJobOptions recurringOptions = builder.AddValidatedOptions<RecurringTransactionJobOptions>(sectionName: RecurringTransactionJobOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddCronJob<RecurringTransactionHandlingJob>(
			group: recurringOptions.Group,
			triggerName: recurringOptions.TriggerName,
			cronExpression: recurringOptions.CronExpression
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
