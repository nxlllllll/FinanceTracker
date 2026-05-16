using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.HealthChecks;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.AccountProjection;
 
public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
 
		builder.Services.AddInfrastructure(configuration: builder.Configuration);
 
		builder.Services.AddScoped<Projection.AccountProjection>();
		builder.Services.AddScoped<AccountEventApplier>();
		builder.Services.AddRabbitMqCore(configuration: builder.Configuration)
			.AddRabbitMqListener<AggregateEventsMessage, AccountEventsConsumer>();
 
		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);
 
		builder.Services.AddWorkerMetrics(workerName: "Worker.AccountProjection");
 
		WebApplication app = builder.Build();
 
		app.MapWorkerEndpoints();
 
		app.Run();
	}
}