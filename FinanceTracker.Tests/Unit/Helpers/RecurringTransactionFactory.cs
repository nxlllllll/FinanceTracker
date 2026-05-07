using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class RecurringTransactionFactory
{
	public static Result<RecurringTransaction, DomainException> Create(
		Guid? userId = null,
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = "Monthly rent",
		bool isActive = true)
	{
		Result<RecurringTransaction, DomainException> result = RecurringTransaction.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.NewGuid(),
			accountId: accountId ?? Guid.NewGuid(),
			categoryId: categoryId ?? Guid.NewGuid(),
			amount: Money.Create(amount: amount, currency: Currency.Create(value: currency).Value).Value,
			direction: direction,
			dayOfMonth: dayOfMonth,
			description: description
		);
		if (result.IsFailure)
			return Result<RecurringTransaction, DomainException>.Failure(error: result.Error!);

		RecurringTransaction recurringTransaction = result.Value!;
		
		if (!isActive)
			recurringTransaction.Deactivate();
		
		return Result<RecurringTransaction, DomainException>.Success(value: recurringTransaction);
	}
}