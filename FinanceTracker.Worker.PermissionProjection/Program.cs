using FinanceTracker.Contracts.Messages;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.PermissionProjection.Consumer;
using FinanceTracker.Worker.PermissionProjection.Projection;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.PermissionProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddScoped<Projection.PermissionProjection>();
		builder.Services.AddScoped<PermissionEventApplier>();

		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, PermissionEventsConsumer>();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.PermissionProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.PermissionProjection");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}
