using FinanceTracker.Core.Repositories.Budget;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetHandler(
	IBudgetWriteRepository budgetWriteRepository
) : IRequestHandler<CreateBudgetCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateBudgetCommand command,
		CancellationToken ct = default)
	{
		Guid budgetId = Guid.NewGuid();

		await budgetWriteRepository.CreateAsync(
			budgetId: budgetId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			currency: command.Currency,
			amount: command.Amount,
			from: command.From,
			to: command.To,
			ct: ct
		);

		return budgetId;
	}
}