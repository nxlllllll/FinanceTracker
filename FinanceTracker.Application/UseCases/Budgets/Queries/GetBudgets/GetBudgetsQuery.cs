using FinanceTracker.Core.Domains.Budget;
using MediatR;

namespace FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgets;

public sealed record GetBudgetsQuery(
	Guid UserId,
	DateTime? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<IReadOnlyList<Budget>>;