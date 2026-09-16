using FinanceTracker.Contracts.Messages;
using FinanceTracker.Infrastructure.Configurations;
using FinanceTracker.Worker.Shared.HealthCheck;
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
		services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
		return services;
	}

	public static IServiceCollection AddRabbitMqListener<TMessage, THandler>(
		this IServiceCollection services)
		where TMessage : class, IRoutableMessage
		where THandler : class, IMessageHandler<TMessage>
	{
		services.AddScoped<THandler>();
		services.AddHostedService<RabbitMqListenerService<TMessage, THandler>>();
		services.AddHostedService<DeadLetterAuditListener<TMessage, THandler>>();
		return services;
	}

	public static IServiceCollection AddRabbitMqHealthCheck(this IServiceCollection services)
	{
		services.AddHealthChecks().AddCheck<RabbitMqHealthCheck>(name: HealthCheckNames.RabbitMq, tags: [HealthCheckTags.Ready, HealthCheckTags.Broker]);
		return services;
	}
}
