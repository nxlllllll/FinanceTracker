using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, PagedResult<RecurringTransactionReadModel>>
{
	public async Task<PagedResult<RecurringTransactionReadModel>> Handle(
		GetRecurringTransactionsQuery query,
		CancellationToken ct = default)
	{
		return await recurringTransactionReadRepository.GetByUserIdAsync(
			userId: query.UserId,
			cursorCreatedAt: query.CursorCreatedAt,
			cursorId: query.CursorId,
			pageSize: query.PageSize,
			ct: ct
		);
	}
}