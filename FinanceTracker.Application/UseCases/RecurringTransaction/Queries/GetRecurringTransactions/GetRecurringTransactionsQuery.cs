using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;

public sealed record GetRecurringTransactionsQuery(
	Guid UserId,
	DateTimeOffset? CursorCreatedAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<Result<PagedResult<RecurringTransactionReadModel>, AppException>>, IUserScopedRequest;
