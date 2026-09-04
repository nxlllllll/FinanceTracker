using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Transfer;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Queries.GetTransfer;

public sealed class GetTransferHandler(
	ITransferReadRepository transferReadRepository
) : IRequestHandler<GetTransferQuery, Result<TransferReadModel, AppException>>
{
	public async Task<Result<TransferReadModel, AppException>> Handle(
		GetTransferQuery query,
		CancellationToken ct = default)
	{
		TransferReadModel? model = await transferReadRepository.GetByIdAsync(
			transferId: query.TransferId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<TransferReadModel, AppException>.Failure(error: new NotFoundException(message: "Transfer not found.", id: query.TransferId));

		return Result<TransferReadModel, AppException>.Success(value: model);
	}
}
