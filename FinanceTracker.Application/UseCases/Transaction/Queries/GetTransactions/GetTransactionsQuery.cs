using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransactions;

public sealed record GetTransactionsQuery(
	Guid UserId,
	Guid AccountId,
	Guid? CategoryId = null,
	DirectionType? Direction = null,
	bool? IsExcluded = null,
	DateTimeOffset? DateFrom = null,
	DateTimeOffset? DateTo = null,
	DateTimeOffset? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<Result<PagedResult<TransactionReadModel>, AppException>>, IUserScopedRequest;
