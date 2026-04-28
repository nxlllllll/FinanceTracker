using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Queries.GetRecurringTransaction;

public sealed class GetRecurringTransactionHandler(
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<GetRecurringTransactionQuery, RecurringTransactionDto>
{
	public async Task<RecurringTransactionDto> Handle(
		GetRecurringTransactionQuery query,
		CancellationToken ct = default)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: query.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId);

		if (recurringTransaction.UserId != query.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: query.RecurringTransactionId);

		return recurringTransaction;
	}
}