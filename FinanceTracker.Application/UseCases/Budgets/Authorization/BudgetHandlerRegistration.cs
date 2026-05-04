using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.UseCases.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.UseCases.Budgets.Authorization;

internal static class BudgetHandlerRegistration
{
	internal static IServiceCollection RegisterBudgetHandlers(this IServiceCollection services)
	{
		services.AddScoped<BudgetLoader>();
		services.AddScoped<IEntityLoader<ChangeBudgetAmountCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());
		services.AddScoped<IEntityLoader<ChangeBudgetPeriodCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());
		services.AddScoped<IEntityLoader<DeleteBudgetCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());

		services.AddScoped<IAuthorizedHandler<ChangeBudgetAmountCommand, Budget, Guid, DomainException>, ChangeBudgetAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget, Guid, DomainException>, ChangeBudgetPeriodHandler>();
		services.AddScoped<IAuthorizedHandler<DeleteBudgetCommand, Budget, Guid, DomainException>, DeleteBudgetHandler>();

		services.AddScoped<IRequestHandler<ChangeBudgetAmountCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeBudgetAmountCommand, Budget, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<ChangeBudgetPeriodCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<ChangeBudgetPeriodCommand, Budget, Guid, DomainException>>();
		services.AddScoped<IRequestHandler<DeleteBudgetCommand, Result<Guid, DomainException>>, AuthorizedHandlerAdapter<DeleteBudgetCommand, Budget, Guid, DomainException>>();

		return services;
	}
}