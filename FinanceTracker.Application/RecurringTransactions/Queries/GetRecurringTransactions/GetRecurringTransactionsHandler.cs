using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransactions;

public sealed class GetRecurringTransactionsHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionsQuery, IReadOnlyList<RecurringTransactionDto>>
{
	public async Task<IReadOnlyList<RecurringTransactionDto>> Handle(
		GetRecurringTransactionsQuery query,
		CancellationToken ct = default
	) => await recurringTransactionReadRepository.GetByUserIdAsync(userId: query.UserId, ct: ct);
}