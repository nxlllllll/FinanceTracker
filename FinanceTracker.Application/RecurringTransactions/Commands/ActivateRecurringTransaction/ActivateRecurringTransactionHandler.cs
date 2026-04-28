using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IRecurringTransactionReadRepository recurringTransactionReadRepository
) : IRequestHandler<ActivateRecurringTransactionCommand>
{
	public async Task Handle(
		ActivateRecurringTransactionCommand command,
		CancellationToken ct = default)
	{
		RecurringTransactionDto recurringTransaction = await recurringTransactionReadRepository.GetByIdAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct)
			?? throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);
		
		if (recurringTransaction.UserId != command.UserId)
			throw new NotFoundException(message: "Recurring transaction not found.", id: command.RecurringTransactionId);

		if (recurringTransaction.IsActive)
			return;

		await recurringTransactionWriteRepository.ActivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);
	}
}