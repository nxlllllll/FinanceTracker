using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Queries.GetTransactions;

public sealed class GetTransactionsHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionsQuery, IReadOnlyList<Transaction>>
{
	public async Task<IReadOnlyList<Transaction>> Handle(
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
			cursorOccurredAt: query.CursorOccurredAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		);
	}
}