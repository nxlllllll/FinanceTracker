using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Operation;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Queries.GetOperationsHistory;

public sealed class GetOperationsHistoryHandler(
	IUserQueryRepository userQueryRepository
) : IRequestHandler<GetOperationsHistoryQuery, Result<PagedResult<Operation>, AppException>>
{
	public async Task<Result<PagedResult<Operation>, AppException>> Handle(
		GetOperationsHistoryQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<Operation>, AppException>.Success(value: await userQueryRepository.GetHistoryAsync(
			userId: query.UserId,
			type: query.Type,
			dateFrom: query.DateFrom,
			dateTo: query.DateTo,
			cursorOccurredAt: query.CursorOccurredAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
