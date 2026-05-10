using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Worker.Shared.RabbitMQ;

namespace FinanceTracker.Worker.AccountProjection;

public sealed class Program
{
	public static void Main(string[] args)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args: args);

		builder.Services.AddInfrastructure(configuration: builder.Configuration);

		builder.Services.AddScoped<Projection.AccountProjection>();
		
		builder.Services.AddOptions<RabbitMqOptions>()
			.BindConfiguration(configSectionPath: RabbitMqOptions.SectionName)
			.ValidateOnStart();

		builder.Services.AddSingleton<RabbitMqConnectionFactory>();
		builder.Services.AddScoped<AccountEventsConsumer>();

		builder.Services.AddHostedService<AccountEventsListenerService>();

		IHost app = builder.Build();
		app.Run();
	}
}