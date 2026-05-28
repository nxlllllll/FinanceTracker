using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(
	Guid UserId,
	DateTimeOffset? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<RecurringTransactionReadModel>>, IUserScopedRequest;