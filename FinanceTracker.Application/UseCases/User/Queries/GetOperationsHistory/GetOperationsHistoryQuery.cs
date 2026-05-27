using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;

public sealed record GetOperationsHistoryQuery(
	Guid UserId,
	OperationFilterType? Type = null,
	DateTimeOffset? DateFrom = null,
	DateTimeOffset? DateTo = null,
	DateTimeOffset? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<OperationRecord>>, IUserScopedRequest;
