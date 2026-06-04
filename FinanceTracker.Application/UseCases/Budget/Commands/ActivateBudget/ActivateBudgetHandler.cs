using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;

public sealed class ActivateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ActivateBudgetCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ActivateBudgetCommand command,
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = budget.Activate();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await budgetWriteRepository.ActivateAsync(budgetId: budget.Id, ct: ct);

		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}