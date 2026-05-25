using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ActivateRecurringTransaction;

public sealed class ActivateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ActivateRecurringTransactionCommand, RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ActivateRecurringTransactionCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.Activate();
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ActivateAsync(recurringTransactionId: command.RecurringTransactionId, ct: ct);
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
