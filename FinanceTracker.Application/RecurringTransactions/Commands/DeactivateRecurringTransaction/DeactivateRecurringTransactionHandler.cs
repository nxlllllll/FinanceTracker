using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;

public sealed class DeactivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransaction, Guid>
{
	public async Task<Guid> HandleAsync(
		DeactivateRecurringTransactionCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		recurringTransaction.Deactivate();
		await recurringTransactionWriteRepository.DeactivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);
		
		return recurringTransaction.Id;
	}
}