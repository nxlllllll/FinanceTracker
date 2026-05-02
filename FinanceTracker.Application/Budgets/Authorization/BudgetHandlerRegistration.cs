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

		services.AddScoped<IAuthorizedHandler<ChangeBudgetAmountCommand, Budget>, ChangeBudgetAmountHandler>();
		services.AddScoped<IAuthorizedHandler<ChangeBudgetPeriodCommand, Budget>, ChangeBudgetPeriodHandler>();
		services.AddScoped<IAuthorizedHandler<DeleteBudgetCommand, Budget>, DeleteBudgetHandler>();

		services.AddScoped<IRequestHandler<ChangeBudgetAmountCommand>, AuthorizedHandlerAdapter<ChangeBudgetAmountCommand, Budget>>();
		services.AddScoped<IRequestHandler<ChangeBudgetPeriodCommand>, AuthorizedHandlerAdapter<ChangeBudgetPeriodCommand, Budget>>();
		services.AddScoped<IRequestHandler<DeleteBudgetCommand>, AuthorizedHandlerAdapter<DeleteBudgetCommand, Budget>>();

		return services;
	}
}