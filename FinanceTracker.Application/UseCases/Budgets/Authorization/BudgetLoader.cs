using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.UseCases.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budgets.Authorization;

public sealed class BudgetLoader(
	IBudgetReadRepository budgetReadRepository
) : IEntityLoader<ChangeBudgetAmountCommand, Budget, NotFoundException>,
	IEntityLoader<ChangeBudgetPeriodCommand, Budget, NotFoundException>,
	IEntityLoader<DeleteBudgetCommand, Budget, NotFoundException>
{
	public Task<Result<Budget, NotFoundException>> LoadAsync(
		ChangeBudgetAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Result<Budget, NotFoundException>> LoadAsync(
		ChangeBudgetPeriodCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Result<Budget, NotFoundException>> LoadAsync(
		DeleteBudgetCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	private async Task<Result<Budget, NotFoundException>> LoadAndAuthorize(Guid budgetId, Guid userId, CancellationToken ct)
	{
		Budget? budget = await budgetReadRepository.GetByIdAsync(budgetId: budgetId, userId: userId, ct);
		if (budget is null || budget.UserId != userId)
			return Result<Budget, NotFoundException>.Failure(error: new NotFoundException(message: "Budget not found.", id: budgetId));

		return Result<Budget, NotFoundException>.Success(value: budget);
	}
}
