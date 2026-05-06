using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

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
		Result<Currency, DomainException> currencyResult = Currency.Create(value: command.Currency);
		if (currencyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: currencyResult.Error!);
 
		Result<Unit, DomainException> result = recurringTransaction.ChangeCurrency(currency: currencyResult.Value!);
		if (result.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await recurringTransactionWriteRepository.ChangeCurrencyAsync(
			recurringTransactionId: command.RecurringTransactionId,
			currency: currencyResult.Value,
			ct: ct
		);
		
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}