using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionQuery, RecurringTransaction>
{
	public async Task<RecurringTransaction> Handle(
		GetRecurringTransactionQuery query,
		CancellationToken ct = default)
	{
		RecurringTransaction recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: query.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId);

		if (recurringTransaction.UserId != query.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId);

		return recurringTransaction;
	}
}