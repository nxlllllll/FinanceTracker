using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryHandler(
	IUserReadRepository userReadRepository
) : IRequestHandler<GetOperationsHistoryQuery, PagedResult<OperationRecord>>
{
	public async Task<PagedResult<OperationRecord>> Handle(
		GetOperationsHistoryQuery query,
		CancellationToken ct = default)
	{
		return await userReadRepository.GetHistoryAsync(
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
