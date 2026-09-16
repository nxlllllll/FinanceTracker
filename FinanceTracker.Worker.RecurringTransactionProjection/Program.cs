using FinanceTracker.Application.Services.Transactions;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumer;
using FinanceTracker.Worker.Shared.HealthCheck;
using FinanceTracker.Worker.Shared.Host;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;
using Microsoft.AspNetCore.Builder;

namespace FinanceTracker.Worker.RecurringTransactionProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args: args);
		builder.AddWorkerDefaults();

		builder.Services.AddScoped<ITransactionCreationService, TransactionCreationService>();

		builder.Services.AddRabbitMqCore()
			.AddRabbitMqListener<RecurringTransactionTriggeredMessage, RecurringTransactionConsumer>()
			.AddRabbitMqHealthCheck();

		WebApplication app = builder.Build();
		app.MapWorkerEndpoints();
		app.Run();
	}
}
