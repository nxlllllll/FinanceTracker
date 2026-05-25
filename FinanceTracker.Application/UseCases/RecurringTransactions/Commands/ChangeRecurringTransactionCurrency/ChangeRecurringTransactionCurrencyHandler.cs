using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.ChangeRecurringTransactionCurrency;

public sealed class ChangeRecurringTransactionCurrencyHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository
) : IAuthorizedHandler<ChangeRecurringTransactionCurrencyCommand, RecurringTransaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeRecurringTransactionCurrencyCommand command,
		RecurringTransaction recurringTransaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = recurringTransaction.ChangeCurrency(currency: command.Currency);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			currency: command.Currency,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}
