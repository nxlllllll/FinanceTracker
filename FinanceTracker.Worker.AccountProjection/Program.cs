using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.Projection;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.AccountProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddScoped<Projection.AccountProjection>();
		builder.Services.AddScoped<AccountEventApplier>();
		builder.Services.AddProjectionRetryOptions();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<AggregateEventsMessage, AccountEventsConsumer>()
			.AddRabbitMqHealthCheck();

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
