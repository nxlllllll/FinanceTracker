using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, IReadOnlyList<RecurringTransaction>>
{
	public async Task<IReadOnlyList<RecurringTransaction>> Handle(
		GetRecurringTransactionsQuery query,
		CancellationToken ct = default
	) => await recurringTransactionReadRepository.GetByUserIdAsync(userId: query.UserId, ct: ct);
}