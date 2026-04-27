using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.CreateBudget;

public sealed record CreateBudgetCommand(
	Guid UserId,
	Guid CategoryId,
	string Currency,
	decimal Amount,
	DateOnly From,
	DateOnly To
) : IRequest<Guid>;