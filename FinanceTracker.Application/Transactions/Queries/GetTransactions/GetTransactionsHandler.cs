using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Queries.GetTransactions;

public sealed class GetTransactionsHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionsQuery, IReadOnlyList<TransactionDto>>
{
	public async Task<IReadOnlyList<TransactionDto>> Handle(
		GetTransactionsQuery query,
		CancellationToken ct = default)
	{
		return await transactionReadRepository.GetAllAsync(
			accountId: query.AccountId,
			categoryId: query.CategoryId,
			direction: query.Direction,
			isExcluded: query.IsExcluded,
			dateFrom: query.DateFrom,
			dateTo: query.DateTo,
			ct: ct
		);
	}
}