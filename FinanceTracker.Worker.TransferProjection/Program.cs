using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.TransferProjection.Consumers;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.TransferProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqListener<AggregateEventsMessage, AccountTransferConsumer>();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.TransferProjection");

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}