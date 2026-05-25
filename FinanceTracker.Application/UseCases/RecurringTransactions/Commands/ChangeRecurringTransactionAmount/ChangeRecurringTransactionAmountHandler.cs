using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionAmount;

public sealed class ChangeRecurringTransactionAmountHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionAmountCommand, RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionAmountCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default
	)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeAmount(amount: command.Amount);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await recurringTransactionWriteRepository.ChangeAmountAsync(
			recurringTransactionId: command.RecurringTransactionId,
			amount: command.Amount,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
