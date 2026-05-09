using FinanceTracker.Application.Behaviours.Validation;
using FinanceTracker.Application.UseCases.Accounts.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Authorization;
using FinanceTracker.Application.UseCases.Categories.Authorization;
using FinanceTracker.Application.UseCases.RecurringTransactions.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Authorization;
using FinanceTracker.Application.UseCases.Transfers.Authorization;
using FinanceTracker.Application.UseCases.Users.Authorization;
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
 
			configuration.AddBehavior(
				serviceType: typeof(IPipelineBehavior<,>),
				implementationType: typeof(ValidationBehavior<,>)
			);
 
			configuration.AddBehavior(
				serviceType: typeof(IPipelineBehavior<,>),
				implementationType: typeof(QueryValidationBehavior<,>)
			);
		});

		services.AddValidatorsFromAssembly(assembly: typeof(DependencyInjection).Assembly);
		
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