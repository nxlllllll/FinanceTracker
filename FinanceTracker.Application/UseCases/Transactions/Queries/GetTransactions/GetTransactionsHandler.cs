using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transactions.Queries.GetTransactions;

public sealed class GetTransactionsHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionsQuery, PagedResult<Transaction>>
{
	public async Task<PagedResult<Transaction>> Handle(
		GetTransactionsQuery query,
		CancellationToken ct = default)
	{
		return await transactionReadRepository.GetAllAsync(
			userId: query.UserId,
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