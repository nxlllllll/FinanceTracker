using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransaction;

public sealed class GetTransactionHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionQuery, TransactionDto?>
{
	public async Task<TransactionDto?> Handle(
		GetTransactionQuery query,
		CancellationToken ct = default
	) => await transactionReadRepository.GetByIdAsync(transactionId: query.TransactionId, ct: ct);
}