using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetOperationsHistoryQuery, PagedResult<Operation>>
{
	public async Task<PagedResult<Operation>> Handle(
		GetOperationsHistoryQuery query,
		CancellationToken ct = default)
	{
		return await userQueryRepository.GetHistoryAsync(
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