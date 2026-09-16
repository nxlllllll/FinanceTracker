using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.PermissionProjection.Consumer;
using FinanceTracker.Worker.PermissionProjection.Projection;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.PermissionProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddScoped<Projection.PermissionProjection>();
		builder.Services.AddScoped<PermissionEventApplier>();
		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, PermissionEventsConsumer>()
			.AddRabbitMqHealthCheck();

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
