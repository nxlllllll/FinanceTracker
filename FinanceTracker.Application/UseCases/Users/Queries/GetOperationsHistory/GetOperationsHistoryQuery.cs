using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;

public sealed record GetOperationsHistoryQuery(
	Guid UserId,
	OperationFilterType? Type = null,
	DateTimeOffset? DateFrom = null,
	DateTimeOffset? DateTo = null,
	DateTimeOffset? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<OperationDto>>, IUserScopedRequest;
