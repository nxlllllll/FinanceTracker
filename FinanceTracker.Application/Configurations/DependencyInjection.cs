using FinanceTracker.Application.Accounts.Authorization;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Behaviours;
using FinanceTracker.Application.Budgets.Authorization;
using FinanceTracker.Application.Categories.Authorization;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Application.RecurringTransactions.Authorization;
using FinanceTracker.Application.Transactions.Authorization;
using FinanceTracker.Application.Transfers.Authorization;
using FinanceTracker.Application.Users.Authorization;
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

		services.RegisterAuthorizedHandlers();
		
		return services;
	}

	private static void RegisterAuthorizedHandlers(this IServiceCollection services)
	{
		services.RegisterAccountHandlers();
		services.RegisterUserHandlers();
		services.RegisterCategoryHandlers();
		services.RegisterTransactionHandlers();
		services.RegisterRecurringTransactionHandlers();
		services.RegisterBudgetHandlers();
		services.RegisterTransferHandlers();
	}
}