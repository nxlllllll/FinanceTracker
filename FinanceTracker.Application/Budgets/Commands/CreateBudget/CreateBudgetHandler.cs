using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.ValueObjects;
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
		Budget budget = Budget.Create(
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: new Money(amount: command.Amount, currency: command.Currency),
			from: command.From,
			to: command.To
		);

		await budgetWriteRepository.CreateAsync(budget: budget, ct: ct);

		return budget.Id;
	}
}