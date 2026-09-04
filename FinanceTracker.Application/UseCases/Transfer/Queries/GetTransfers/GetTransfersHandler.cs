using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfers;

public sealed class GetTransfersHandler(
	ITransferReadRepository transferReadRepository
) : IRequestHandler<GetTransfersQuery, Result<PagedResult<TransferReadModel>, AppException>>
{
	public async Task<Result<PagedResult<TransferReadModel>, AppException>> Handle(
		GetTransfersQuery query,
		CancellationToken ct = default)
	{
		return Result<PagedResult<TransferReadModel>, AppException>.Success(value: await transferReadRepository.GetAllAsync(
			userId: query.UserId,
			accountId: query.AccountId,
			status: query.Status,
			dateFrom: query.DateFrom,
			dateTo: query.DateTo,
			cursorOccurredAt: query.CursorOccurredAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		));
	}
}
