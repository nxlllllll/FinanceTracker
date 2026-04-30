using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.RecurringTransaction;

namespace FinanceTracker.Application.RecurringTransactions.Commands.DeactivateRecurringTransaction;

public sealed class DeactivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<DeactivateRecurringTransactionCommand, RecurringTransactionDto>
{
	public async Task HandleAsync(
		DeactivateRecurringTransactionCommand command,
		RecurringTransactionDto recurringTransaction,
		CancellationToken ct = default)
	{
		if (!recurringTransaction.IsActive)
			return;

		await recurringTransactionWriteRepository.DeactivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);
	}
}