using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Operation;
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
) : IRequest<Result<PagedResult<Operation>, AppException>>, IUserScopedRequest;
