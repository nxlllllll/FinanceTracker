using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction>>
{
	public async Task<PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction>> Handle(
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
