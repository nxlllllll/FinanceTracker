using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Authorization;

public sealed class BudgetLoader(
	IBudgetReadRepository budgetReadRepository
) : IEntityLoader<ChangeBudgetAmountCommand, Budget>,
	IEntityLoader<ChangeBudgetPeriodCommand, Budget>,
	IEntityLoader<DeleteBudgetCommand, Budget>
{
	public Task<Budget> LoadAsync(
		ChangeBudgetAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Budget> LoadAsync(
		ChangeBudgetPeriodCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Budget> LoadAsync(
		DeleteBudgetCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	private async Task<Budget> LoadAndAuthorize(Guid budgetId, Guid userId, CancellationToken ct)
	{
		Budget budget = await budgetReadRepository.GetByIdAsync(budgetId: budgetId, userId: userId, ct)
			?? throw new NotFoundException(message: "Budget not found.", id: budgetId);

		if (budget.UserId != userId)
			throw new NotFoundException(message: "Budget not found.", id: budgetId);

		return budget;
	}
}