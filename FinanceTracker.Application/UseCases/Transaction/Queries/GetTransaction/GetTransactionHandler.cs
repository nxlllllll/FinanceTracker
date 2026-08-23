using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.ReadModels.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed class GetTransactionHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionQuery, Result<TransactionReadModel, AppException>>
{
	public async Task<Result<TransactionReadModel, AppException>> Handle(
		GetTransactionQuery query,
		CancellationToken ct = default)
	{
		TransactionReadModel? model = await transactionReadRepository.GetByIdAsync(
			transactionId: query.TransactionId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<TransactionReadModel, AppException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: query.TransactionId));

		return Result<TransactionReadModel, AppException>.Success(value: model);
	}
}
