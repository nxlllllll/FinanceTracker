using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction, Guid>
{
	public async Task<Guid> HandleAsync(
		ActivateRecurringTransactionCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		recurringTransaction.Activate();
		await recurringTransactionWriteRepository.ActivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);

		return recurringTransaction.Id;
	}
}