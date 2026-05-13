using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;

namespace FinanceTracker.Worker.RecurringTransactionProjection;
 
public sealed class Program
{
	public static void Main(string[] args)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);
 
		builder.Services.AddInfrastructure(configuration: builder.Configuration);
 
		builder.Services.AddScoped<ITransactionCreationService, TransactionCreationService>();
		builder.Services.AddRabbitMqCore(configuration: builder.Configuration);
		builder.Services.AddRabbitMqListener<RecurringTransactionTriggeredMessage, RecurringTransactionConsumer>();
 
		IHost app = builder.Build();
		app.Run();
	}
}