using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgets;

public sealed record GetBudgetsQuery(
	Guid UserId,
	DateTime? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<Core.Domains.Budget.Budget>>, IUserScopedRequest;