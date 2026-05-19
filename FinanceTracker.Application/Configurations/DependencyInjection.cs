using System.Reflection;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Correlation;
using FinanceTracker.Application.Behaviours.Idempotency;
using FinanceTracker.Application.Behaviours.Retry;
using FinanceTracker.Application.Behaviours.Tracing;
using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Accounts.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Authorization;
using FinanceTracker.Application.UseCases.Categories.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransactions.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Authorization;
using FinanceTracker.Application.UseCases.Transfers.Authorization;
using FinanceTracker.Application.UseCases.Users.Authorization;
using FinanceTracker.Core.Results;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddOptions<RetryOptions>()
			.BindConfiguration(configSectionPath: RetryOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		
		services.AddOptions<IdempotencyOptions>()
			.BindConfiguration(configSectionPath: IdempotencyOptions.SectionName)
			.ValidateDataAnnotations()
			.ValidateOnStart();
		
		services.AddMediatR(configuration: cfg =>
		{
			cfg.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly);

			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(TracingBehavior<,>));
			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(CorrelationBehavior<,>));
			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(ConcurrencyRetryBehavior<,>));
			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(IdempotencyBehavior<,>));
			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(ValidationBehavior<,>));
			cfg.AddBehavior(serviceType: typeof(IPipelineBehavior<,>), implementationType: typeof(QueryValidationBehavior<,>));
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

			foreach (Type handlerInterface in type.GetInterfaces().Where(predicate: i => i.IsGenericType && i.GetGenericTypeDefinition() == authorizedHandlerOpen))
			{
				Type[] args = handlerInterface.GetGenericArguments();

				services.AddScoped(serviceType: handlerInterface, implementationType: type);
				services.AddScoped(
					serviceType: requestHandlerOpen.MakeGenericType(args[0], resultOpen.MakeGenericType(args[2], args[3])),
					implementationType: adapterOpen.MakeGenericType(typeArguments: args)
				);
			}
		}
	}
}