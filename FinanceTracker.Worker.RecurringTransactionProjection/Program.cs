using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using FinanceTracker.Worker.Shared.RabbitMQ;

namespace FinanceTracker.Worker.RecurringTransactionProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddScoped<ITransactionCreationService, TransactionCreationService>();

		builder.Services.AddOptions<RabbitMqOptions>()
			.BindConfiguration(configSectionPath: RabbitMqOptions.SectionName)
			.ValidateOnStart();

		builder.Services.AddSingleton<RabbitMqConnectionFactory>();
		builder.Services.AddScoped<RecurringTransactionConsumer>();

		builder.Services.AddHostedService<RecurringTransactionListenerService>();

		IHost app = builder.Build();
		app.Run();
	}
}