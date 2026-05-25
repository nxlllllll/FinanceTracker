using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Users.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryHandler(
	IUserReadRepository userReadRepository
) : IRequestHandler<GetOperationsHistoryQuery, PagedResult<OperationDto>>
{
	public async Task<PagedResult<OperationDto>> Handle(
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
