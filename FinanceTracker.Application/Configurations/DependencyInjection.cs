using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Application.Behaviours.Tracing;
using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Core.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddOptions<RetryOptions>()
			.BindConfiguration(configSectionPath: RetryOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<IdempotencyOptions>()
			.BindConfiguration(configSectionPath: IdempotencyOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<RateLimitOptions>()
			.BindConfiguration(configSectionPath: RateLimitOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<AnonymousRateLimitOptions>()
			.BindConfiguration(configSectionPath: AnonymousRateLimitOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddOptions<MoneyLimitsOptions>()
			.BindConfiguration(configSectionPath: MoneyLimitsOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();

		services.AddScoped<PostCommitNotificationCollector>();
		services.AddScoped<IPostCommitNotifications>(implementationFactory: sp => sp.GetRequiredService<PostCommitNotificationCollector>());
		services.AddScoped<IPostCommitNotificationSink>(implementationFactory: sp => sp.GetRequiredService<PostCommitNotificationCollector>());

		services.AddScoped<ITransactionCreationService, TransactionCreationService>();

		services.AddMediatR(configuration: cfg =>
		{
			cfg.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly);

			cfg.AddOpenBehavior(openBehaviorType: typeof(TracingBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(CorrelationBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(AuthRateLimitingBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(RateLimitingBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(ValidationBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(PostCommitNotificationBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(IdempotencyBehaviour<,>));
			cfg.AddOpenBehavior(openBehaviorType: typeof(ConcurrencyRetryBehaviour<,>));
		});

		services.AddValidatorsFromAssembly(assembly: typeof(DependencyInjection).Assembly);

		services.RegisterAuthorizedHandlers();

		return services;
	}

	private static void RegisterAuthorizedHandlers(this IServiceCollection services)
	{
		Assembly assembly = typeof(DependencyInjection).Assembly;

		Type entityLoaderOpen = typeof(IEntityLoader<,,>);
		Type authorizedHandlerOpen = typeof(IAuthorizedHandler<,,,>);
		Type adapterOpen = typeof(AuthorizedHandlerAdapter<,,,>);

		Type noEntityLoaderOpen = typeof(IEntityLoader<,>);
		Type noEntityAuthorizedHandlerOpen = typeof(IAuthorizedHandler<,,>);
		Type noEntityAdapterOpen = typeof(AuthorizedHandlerAdapter<,,>);

		Type requestHandlerOpen = typeof(IRequestHandler<,>);
		Type resultOpen = typeof(Result<,>);

		foreach (Type type in assembly.GetTypes().Where(predicate: t => t is { IsClass: true, IsAbstract: false }))
		{
			Type[] loaderInterfaces = type.GetInterfaces().Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == entityLoaderOpen).ToArray();

			if (loaderInterfaces.Length > 0)
			{
				services.TryAddScoped(service: type);
				foreach (Type iface in loaderInterfaces)
					services.AddScoped(serviceType: iface, implementationFactory: sp => sp.GetRequiredService(serviceType: type));
			}

			Type[] noEntityLoaderInterfaces = type.GetInterfaces().Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == noEntityLoaderOpen).ToArray();

			if (noEntityLoaderInterfaces.Length > 0)
			{
				services.TryAddScoped(service: type);
				foreach (Type iface in noEntityLoaderInterfaces)
					services.AddScoped(serviceType: iface, implementationFactory: sp => sp.GetRequiredService(serviceType: type));
			}

			foreach (Type handlerInterface in type.GetInterfaces().Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen))
			{
				Type[] args = handlerInterface.GetGenericArguments();

				services.AddScoped(serviceType: handlerInterface, implementationType: type);
				services.AddScoped(
					serviceType: requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[2], args[3])),
					implementationType: adapterOpen.MakeGenericType(typeArguments: args)
				);
			}

			foreach (Type handlerInterface in type.GetInterfaces().Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == noEntityAuthorizedHandlerOpen))
			{
				Type[] args = handlerInterface.GetGenericArguments();

				services.AddScoped(serviceType: handlerInterface, implementationType: type);
				services.AddScoped(
					serviceType: requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[1], args[2])),
					implementationType: noEntityAdapterOpen.MakeGenericType(typeArguments: args)
				);
			}
		}
	}
}
