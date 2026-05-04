using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Domains.Budget;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Application.Budgets.Authorization;

internal static class BudgetHandlerRegistration
{
	internal static IServiceCollection RegisterBudgetHandlers(this IServiceCollection services)
	{
		services.AddScoped<BudgetLoader>();
		services.AddScoped<IEntityLoader<ChangeBudgetAmountCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());
		services.AddScoped<IEntityLoader<ChangeBudgetPeriodCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());
		services.AddScoped<IEntityLoader<DeleteBudgetCommand, Budget>>(sp => sp.GetRequiredService<BudgetLoader>());

		services.AddScoped<IAuthorizedHandler<ChangeBudgetAmountCommand, Budget, Guid>, ChangeBudgetAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget, Guid>, ChangeBudgetPeriodHandler>();
		services.AddScoped<IAuthorizedHandler<DeleteBudgetCommand, Budget, Guid>, DeleteBudgetHandler>();

		services.AddScoped<IRequestHandler<ChangeBudgetAmountCommand, Guid>, AuthorizedHandlerAdapter<ChangeBudgetAmountCommand, Budget, Guid>>();
		services.AddScoped<IRequestHandler<ChangeBudgetPeriodCommand, Guid>, AuthorizedHandlerAdapter<ChangeBudgetPeriodCommand, Budget, Guid>>();
		services.AddScoped<IRequestHandler<DeleteBudgetCommand, Guid>, AuthorizedHandlerAdapter<DeleteBudgetCommand, Budget, Guid>>();

		return services;
	}
}