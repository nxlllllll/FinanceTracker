using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;

public sealed record ChangeBudgetAmountCommand(
	Guid UserId,
	Guid BudgetId,
	decimal Amount
) : IRequest;