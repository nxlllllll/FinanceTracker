using FinanceTracker.Application.Behaviours.Authorization;
using MediatR;

namespace FinanceTracker.Application.Budgets.Commands.DeleteBudget;

public sealed record DeleteBudgetCommand(
	Guid UserId,
	Guid BudgetId
) : IRequest<Guid>, IAuthorizable;