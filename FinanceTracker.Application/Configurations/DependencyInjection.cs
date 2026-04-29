using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Behaviours;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Core.Domains.Abstractions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Configurations;

public static class DependencyInjection
{
	public static IServiceCollection AddApplication(this IServiceCollection services)
	{
		services.AddMediatR(configuration: configuration =>
		{
			configuration.RegisterServicesFromAssembly(assembly: typeof(DependencyInjection).Assembly);
			configuration.AddBehavior(serviceType: typeof(IPipelineBehavior<,>),
				implementationType: typeof(ValidationBehavior<,>));
		});

		services.AddValidatorsFromAssembly(assembly: typeof(DependencyInjection).Assembly);
		services.AddScoped<INotificationDispatcher, MediatRNotificationDispatcher>();

		services.AddSingleton<IAggregateNotificationFactory, AccountNotificationFactory>();

		return services;
	}
}