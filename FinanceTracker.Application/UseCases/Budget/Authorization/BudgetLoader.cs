using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budget.Authorization;

public sealed class BudgetLoader(
	IBudgetRepository budgetRepository
) : IEntityLoader<ChangeBudgetAmountCommand, Core.Domains.Budget.Budget, AppException>,
	IEntityLoader<ChangeBudgetPeriodCommand, Core.Domains.Budget.Budget, AppException>,
	IEntityLoader<DeactivateBudgetCommand, Core.Domains.Budget.Budget, AppException>,
	IEntityLoader<ActivateBudgetCommand, Core.Domains.Budget.Budget, AppException>
{
	public Task<Result<Core.Domains.Budget.Budget, AppException>> LoadAsync(
		ChangeBudgetAmountCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Budget.Budget, AppException>> LoadAsync(
		ChangeBudgetPeriodCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Budget.Budget, AppException>> LoadAsync(
		DeactivateBudgetCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	public Task<Result<Core.Domains.Budget.Budget, AppException>> LoadAsync(
		ActivateBudgetCommand request,
		CancellationToken ct
	) => LoadAndAuthorize(budgetId: request.BudgetId, userId: request.UserId, ct: ct);

	private async Task<Result<Core.Domains.Budget.Budget, AppException>> LoadAndAuthorize(Guid budgetId, Guid userId, CancellationToken ct)
	{
		Core.Domains.Budget.Budget? budget = await budgetRepository.GetByIdAsync(budgetId: budgetId, userId: userId, ct);
		if (budget is null || budget.UserId != userId)
			return Result<Core.Domains.Budget.Budget, AppException>.Failure(error: new NotFoundException(message: "Budget not found.", id: budgetId));

		return Result<Core.Domains.Budget.Budget, AppException>.Success(value: budget);
	}
}
