using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(
	Guid UserId,
	DateTimeOffset? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction>>, IUserScopedRequest;
