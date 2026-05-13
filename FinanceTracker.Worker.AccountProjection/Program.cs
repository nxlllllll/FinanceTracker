using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Worker.Shared.RabbitMQ.Configuration;

namespace FinanceTracker.Worker.AccountProjection;
 
public sealed class Program
{
	public static void Main(string[] args)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);
 
		builder.Services.AddInfrastructure(configuration: builder.Configuration);
 
		builder.Services.AddScoped<Projection.AccountProjection>();
		builder.Services.AddRabbitMqCore(configuration: builder.Configuration);
		builder.Services.AddRabbitMqListener<AggregateEventsMessage, AccountEventsConsumer>();
 
		IHost app = builder.Build();
		app.Run();
	}
}