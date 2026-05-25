using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using FinanceTracker.Worker.TransferProjection.Consumers;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.TransferProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, AccountTransferConsumer, Account>();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.TransferProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.TransferProjection");
		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}