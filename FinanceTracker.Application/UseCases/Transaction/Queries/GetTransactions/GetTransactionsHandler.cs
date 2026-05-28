using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Queries.GetTransactions;

public sealed class GetTransactionsHandler(
	ITransactionReadRepository transactionReadRepository
) : IRequestHandler<GetTransactionsQuery, PagedResult<TransactionReadModel>>
{
	public async Task<PagedResult<TransactionReadModel>> Handle(
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