using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;

namespace FinanceTracker.Application.Budgets.Authorization;

public sealed class BudgetLoader(
	IBudgetReadRepository budgetReadRepository
) : IEntityLoader<ChangeBudgetAmountCommand, BudgetDto>,
	IEntityLoader<ChangeBudgetPeriodCommand, BudgetDto>,
	IEntityLoader<DeleteBudgetCommand, BudgetDto>
{
	public Task<BudgetDto> LoadAsync(
		ChangeBudgetAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<BudgetDto> LoadAsync(
		ChangeBudgetPeriodCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<BudgetDto> LoadAsync(
		DeleteBudgetCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	private async Task<BudgetDto> LoadAndAuthorize(Guid budgetId, Guid userId, CancellationToken ct)
	{
		BudgetDto budget = await budgetReadRepository.GetByIdAsync(budgetId: budgetId, userId: userId, ct)
			?? throw new NotFoundException(message: "Budget not found.", id: budgetId);

		if (budget.UserId != userId)
			throw new NotFoundException(message: "Budget not found.", id: budgetId);

		return budget;
	}
}