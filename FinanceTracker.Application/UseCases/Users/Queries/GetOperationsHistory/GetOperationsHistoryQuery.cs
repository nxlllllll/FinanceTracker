using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;

public sealed record GetOperationsHistoryQuery(
	Guid UserId,
	OperationFilterType? Type = null,
	DateTime? DateFrom = null,
	DateTime? DateTo = null,
	DateTime? CursorOccurredAt = null,
	Guid? CursorId = null,
	int PageSize = 20
) : IRequest<PagedResult<OperationDto>>, IUserScopedRequest;