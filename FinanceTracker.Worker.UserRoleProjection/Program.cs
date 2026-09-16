using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using FinanceTracker.Worker.UserRoleProjection.Consumer;
using FinanceTracker.Worker.UserRoleProjection.Projection;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.UserRoleProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddScoped<Projection.UserRoleProjection>();
		builder.Services.AddScoped<UserRoleEventApplier>();
		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, UserRoleEventsConsumer>()
			.AddRabbitMqHealthCheck();

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
