using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Services.TransferCompensation;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Quartz;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using FinanceTracker.Worker.TransferProjection.Consumer;
using FinanceTracker.Worker.TransferProjection.Job;
using FinanceTracker.Worker.TransferProjection.Services;
using Microsoft.AspNetCore.Builder;
using Quartz;

namespace FinanceTracker.Worker.TransferProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddScoped<ITransferCompensationService, TransferCompensationService>();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, AccountTransferConsumer>();

		builder.Services.AddOptions<TransferCreditLagOptions>()
			.BindConfiguration(configSectionPath: TransferCreditLagOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		TransferCreditLagOptions lagOptions = builder.Configuration
			.GetSection(key: TransferCreditLagOptions.SectionName)
			.Get<TransferCreditLagOptions>() ?? new TransferCreditLagOptions();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddQuartz(configure: q =>
		{
			q.UseClusteredPostgresStore(connectionString: connectionString, schedulerName: "TransferProjectionScheduler");

			q.AddJob<TransferCreditLagJob>(configure: j => j.WithIdentity(name: nameof(TransferCreditLagJob), group: lagOptions.Group));
			q.AddTrigger(configure: t => t
				.ForJob(jobName: nameof(TransferCreditLagJob), jobGroup: lagOptions.Group)
				.WithIdentity(name: lagOptions.TriggerName, group: lagOptions.Group)
				.WithSimpleSchedule(action: s => s.WithIntervalInMinutes(minutes: lagOptions.IntervalMinutes).RepeatForever())
			);
		});

		builder.Services.AddQuartzHostedService(configure: options => options.WaitForJobsToComplete = true);

		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"])
			.AddCheck<QuartzHealthCheck>(name: "quartz", tags: ["ready", "scheduler"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.TransferProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.TransferProjection");

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}