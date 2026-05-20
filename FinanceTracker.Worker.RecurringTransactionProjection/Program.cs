using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.RecurringTransactionProjection;
 
public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
 
		builder.Services.AddInfrastructure(configuration: builder.Configuration);
 
		builder.Services.AddScoped<ITransactionCreationService, TransactionCreationService>();
		builder.Services.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqListener<RecurringTransactionTriggeredMessage, RecurringTransactionConsumer>();
 
		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);
 
		builder.Services.AddWorkerMetrics(workerName: "Worker.RecurringTransactionProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.RecurringTransactionProjection");
		WebApplication app = builder.Build();
 
		app.MapWorkerEndpoints();
 
		app.Run();
	}
}