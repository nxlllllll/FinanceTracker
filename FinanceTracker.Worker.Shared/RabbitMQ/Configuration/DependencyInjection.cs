using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Configuration;

public static class DependencyInjection
{
	public static IServiceCollection AddRabbitMqCore(this IServiceCollection services)
	{
		services.AddOptions<RabbitMqOptions>()
			.BindConfiguration(configSectionPath: RabbitMqOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddSingleton<RabbitMqConnectionFactory>();

		return services;
	}

	public static IServiceCollection AddRabbitMqPublisher(this IServiceCollection services)
	{
		services.AddScoped<IRabbitMqPublisher, RabbitMqPublisher>();
		return services;
	}

	public static IServiceCollection AddRabbitMqListener<TMessage, THandler>(
		this IServiceCollection services)
		where TMessage : class, IRoutableMessage
		where THandler : class, IMessageHandler<TMessage>
	{
		services.AddScoped<THandler>();
		services.AddHostedService<RabbitMqListenerService<TMessage, THandler>>();
		return services;
	}
}