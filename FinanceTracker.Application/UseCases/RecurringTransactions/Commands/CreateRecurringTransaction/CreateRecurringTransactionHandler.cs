using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.RecurringTransactions.Commands.CreateRecurringTransaction;

public sealed class CreateRecurringTransactionHandler(
	IRecurringTransactionWriteRepository recurringTransactionWriteRepository,
	IDateProvider dateProvider
) : IAuthorizedHandler<CreateRecurringTransactionCommand, Account, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateRecurringTransactionCommand command,
		Account account,
		CancellationToken ct = default)
	{
		Result<Currency, DomainException> currencyResult = Currency.Create(value: command.Currency);
		if (currencyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: currencyResult.Error!);
 
		Result<Money, DomainException> moneyResult = Money.Positive(amount: command.Amount, currency: currencyResult.Value!);
		if (moneyResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: moneyResult.Error!);
 
		Result<RecurringTransaction, DomainException> rtResult = RecurringTransaction.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			accountId: command.AccountId,
			categoryId: command.CategoryId,
			amount: moneyResult.Value!,
			direction: command.Direction,
			dayOfMonth: command.DayOfMonth,
			description: command.Description
		);
		if (rtResult.IsFailure) 
			return Result<Guid, DomainException>.Failure(error: rtResult.Error!);
 
		RecurringTransaction recurringTransaction = rtResult.Value!;
		await recurringTransactionWriteRepository.CreateAsync(recurringTransaction: recurringTransaction, ct: ct);
		return Result<Guid, DomainException>.Success(value: recurringTransaction.Id);
	}
}