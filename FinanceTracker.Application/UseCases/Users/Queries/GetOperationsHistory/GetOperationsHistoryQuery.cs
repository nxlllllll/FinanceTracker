using FinanceTracker.Core.Dtos;
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
) : IRequest<IReadOnlyList<OperationDto>>;