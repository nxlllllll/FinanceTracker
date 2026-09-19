using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.TransferProjection.Consumer;
using FinanceTracker.Worker.TransferProjection.Job;
using FinanceTracker.Worker.TransferProjection.Services;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.TransferProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddScoped<ITransferCompensationService, TransferCompensationService>();
		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, AccountTransferConsumer>()
			.AddRabbitMqHealthCheck();

		TransferCreditLagOptions lagOptions = builder.AddValidatedOptions<TransferCreditLagOptions>(sectionName: TransferCreditLagOptions.SectionName);

		builder.AddWorkerQuartz(configureJobs: quartz => quartz.AddIntervalJob<TransferCreditLagJob>(
			group: lagOptions.Group,
			triggerName: lagOptions.TriggerName,
			interval: TimeSpan.FromMinutes(value: lagOptions.IntervalMinutes)
		));

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
