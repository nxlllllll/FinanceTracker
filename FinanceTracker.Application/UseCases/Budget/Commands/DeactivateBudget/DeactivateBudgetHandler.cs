using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;

public sealed class DeactivateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<DeactivateBudgetCommand, Core.Domains.Budget.Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		DeactivateBudgetCommand command,
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = budget.Deactivate();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await budgetWriteRepository.DeactivateAsync(budgetId: budget.Id, ct: ct);
		
		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}