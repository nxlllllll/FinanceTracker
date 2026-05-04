using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, IReadOnlyList<RecurringTransaction>>
{
	public async Task<IReadOnlyList<RecurringTransaction>> Handle(
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