using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransaction;

public sealed class GetTransactionHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionQuery, Core.Domains.Transaction.Transaction?>
{
	public async Task<Core.Domains.Transaction.Transaction?> Handle(
		GetTransactionQuery query,
		CancellationToken ct = default
	) => await transactionReadRepository.GetByIdAsync(transactionId: query.TransactionId, userId: query.UserId, ct: ct);
}
