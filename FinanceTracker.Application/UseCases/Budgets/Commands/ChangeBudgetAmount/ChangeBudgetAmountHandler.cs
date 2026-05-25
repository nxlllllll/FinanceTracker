using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetAmount;

public sealed class ChangeBudgetAmountHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<ChangeBudgetAmountCommand, Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeBudgetAmountCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = budget.ChangeAmount(amount: command.Amount);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await budgetWriteRepository.ChangeAmountAsync(budgetId: budget.Id, amount: command.Amount, ct: ct);
		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}
