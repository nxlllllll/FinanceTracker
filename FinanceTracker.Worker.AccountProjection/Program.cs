using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
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

		builder.Services.AddOptions<ProjectionRetryOptions>()
			.BindConfiguration(configSectionPath: ProjectionRetryOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, AccountEventsConsumer>();
 
		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;
 
		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);
 
		builder.Services.AddWorkerMetrics(workerName: "Worker.AccountProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.AccountProjection");
		
		WebApplication app = builder.Build();
 
		app.MapWorkerEndpoints();
 
		app.Run();
	}
}