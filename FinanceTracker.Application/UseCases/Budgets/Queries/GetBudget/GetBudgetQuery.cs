using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid UserId,
	Guid BudgetId
) : IRequest<Budget?>, IUserScopedRequest;