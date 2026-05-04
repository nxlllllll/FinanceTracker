using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IAuthorizedHandler<DeleteBudgetCommand, Budget, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		DeleteBudgetCommand command,
		Budget budget,
		CancellationToken ct = default)
	{
		await budgetWriteRepository.DeleteAsync(budgetId: budget.Id, ct: ct);
		
		return Result<Guid, DomainException>.Success(value: budget.Id);
	}
}