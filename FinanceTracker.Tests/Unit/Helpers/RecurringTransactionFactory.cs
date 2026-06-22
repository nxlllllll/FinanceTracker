using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
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
			userId: userId ?? Guid.CreateVersion7(),
			accountId: accountId ?? Guid.CreateVersion7(),
			categoryId: categoryId ?? Guid.CreateVersion7(),
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

	public static RecurringTransactionReadModel CreateReadModel(
		Guid? id = null,
		Guid? userId = null,
		Guid? accountId = null,
		Guid? categoryId = null,
		decimal amount = 5000m,
		string currency = "RUB",
		DirectionType direction = DirectionType.Debit,
		int dayOfMonth = 15,
		string? description = "Monthly rent",
		bool isActive = true,
		int rowVersion = 0,
		DateTimeOffset? lastExecutedAt = null,
		DateTimeOffset? lastMissedAt = null)
	{
		Currency curr = Currency.Reconstitute(value: currency);

		return new RecurringTransactionReadModel(
			Id: id ?? Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			AccountId: accountId ?? Guid.CreateVersion7(),
			CategoryId: categoryId ?? Guid.CreateVersion7(),
			Amount: Money.Reconstitute(amount: amount, currency: curr),
			Direction: direction,
			DayOfMonth: dayOfMonth,
			Description: description,
			IsActive: isActive,
			RowVersion: rowVersion,
			LastExecutedAt: lastExecutedAt,
			LastMissedAt: lastMissedAt,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}