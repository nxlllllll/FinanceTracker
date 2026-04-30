using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransaction;

public sealed class GetTransactionHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionQuery, Transaction?>
{
	public async Task<Transaction?> Handle(
		GetTransactionQuery query,
		CancellationToken ct = default
	) => await transactionReadRepository.GetByIdAsync(transactionId: query.TransactionId, ct: ct);
}