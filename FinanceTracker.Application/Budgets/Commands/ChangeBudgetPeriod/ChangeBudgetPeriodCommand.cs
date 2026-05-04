using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;

public sealed record ChangeBudgetPeriodCommand(
	Guid UserId,
	Guid BudgetId,
	DateOnly From,
	DateOnly To
) : IRequest<Guid>, IAuthorizable;