using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed class GetTransactionHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionQuery, Result<TransactionReadModel, DomainException>>
{
	public async Task<Result<TransactionReadModel, DomainException>> Handle(
		GetTransactionQuery query,
		CancellationToken ct = default)
	{
		TransactionReadModel? model = await transactionReadRepository.GetByIdAsync(
			transactionId: query.TransactionId,
			userId: query.UserId,
			ct: ct
		);

		if (model is null)
			return Result<TransactionReadModel, DomainException>.Failure(error: new NotFoundException(message: "Transaction not found.", id: query.TransactionId));

		return Result<TransactionReadModel, DomainException>.Success(value: model);
	}
}
