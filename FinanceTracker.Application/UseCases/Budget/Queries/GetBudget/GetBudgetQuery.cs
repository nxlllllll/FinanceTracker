using FinanceTracker.Application.Behaviours.RateLimit;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;

public sealed record GetBudgetQuery(
	Guid UserId,
	Guid BudgetId
) : IRequest<Core.Domains.Budget.Budget?>, IUserScopedRequest;
