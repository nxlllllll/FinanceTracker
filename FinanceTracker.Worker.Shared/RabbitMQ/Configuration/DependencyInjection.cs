using FinanceTracker.Contracts.Messages;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using FinanceTracker.Worker.Shared.RabbitMQ.Retry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

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
		services.AddSingleton<IRetryCounter>(implementationFactory: sp =>
		{
			IOptions<RabbitMqOptions> rabbitMqOptions = sp.GetRequiredService<IOptions<RabbitMqOptions>>();
			IConnectionMultiplexer? multiplexer = sp.GetService<IConnectionMultiplexer>();
			ILogger<RedisRetryCounter> logger = sp.GetRequiredService<ILogger<RedisRetryCounter>>();

			if (multiplexer is null)
			{
				logger.LogWarning(message: "[RetryCounter] IConnectionMultiplexer not registered. Falling back to in-memory retry counter. Counts will be lost on restart.");
				return new InMemoryRetryCounter(options: rabbitMqOptions);
			}

			try
			{
				multiplexer.GetDatabase();
				return new RedisRetryCounter(connectionMultiplexer: multiplexer, options: rabbitMqOptions);
			}
			catch (Exception ex)
			{
				logger.LogWarning(exception: ex, message: "[RetryCounter] Redis unavailable. Falling back to in-memory retry counter. Counts will be lost on restart.");
				return new InMemoryRetryCounter(options: rabbitMqOptions);
			}
		});

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