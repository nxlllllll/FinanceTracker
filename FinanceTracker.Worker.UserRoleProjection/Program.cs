using FinanceTracker.Contracts.Messages;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.Shared.Tracing;
using FinanceTracker.Worker.UserRoleProjection.Consumer;
using FinanceTracker.Worker.UserRoleProjection.Projection;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.UserRoleProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);

		builder.AddStructuredLogging();

		builder.UseStrictDependencyValidation();

		builder.Services.AddPersistence(configuration: builder.Configuration);

		builder.Services.AddScoped<Projection.UserRoleProjection>();
		builder.Services.AddScoped<UserRoleEventApplier>();

		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, UserRoleEventsConsumer>();

		string connectionString = builder.Configuration.GetConnectionString(name: "FinanceTrackerContext")!;
		string redisConnectionString = builder.Configuration.GetSection(key: "Redis")["ConnectionString"]!;

		builder.Services.AddWorkerHealthChecks(connectionString: connectionString, redisConnectionString: redisConnectionString)
			.AddCheck<RabbitMqHealthCheck>(name: "rabbitmq", tags: ["ready", "broker"]);

		builder.Services.AddWorkerMetrics(workerName: "Worker.UserRoleProjection");
		builder.Services.AddWorkerTracing(workerName: "Worker.UserRoleProjection");

		WebApplication app = builder.Build();

		app.MapWorkerEndpoints();

		app.Run();
	}
}
