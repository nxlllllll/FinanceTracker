using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Operations;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryHandler(
	IOperationsReadRepository operationsReadRepository
) : IRequestHandler<GetOperationsHistoryQuery, IReadOnlyList<OperationDto>>
{
	public async Task<IReadOnlyList<OperationDto>> Handle(
		GetOperationsHistoryQuery query,
		CancellationToken ct = default)
	{
		return await operationsReadRepository.GetHistoryAsync(
			userId: query.UserId,
			type: query.Type,
			dateFrom: query.DateFrom,
			dateTo: query.DateTo,
			cursorOccurredAt: query.CursorOccurredAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		);
	}
}