using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

/// <summary>
/// Represents a monthly recurring transaction that is automatically triggered
/// on a specific day of month by <c>RecurringTransactionHandlingJob</c>.
/// </summary>
public sealed class RecurringTransaction
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public Guid AccountId { get; private set; }
	public Guid CategoryId { get; private set; }
	public Money Amount { get; private set; }
	public DirectionType Direction { get; private set; }
	public int DayOfMonth { get; private set; }
	public string? Description { get; private set; }
	public bool IsActive { get; private set; }
	public DateTimeOffset? LastExecutedAt { get; private set; }
	public DateTimeOffset? LastMissedAt { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

	private RecurringTransaction() { }

	public static Result<RecurringTransaction, DomainException> Create(
		DateTimeOffset createdAt,
		Guid userId,
		Guid accountId,
		Guid categoryId,
		Money amount,
		DirectionType direction,
		int dayOfMonth,
		string? description)
	{
		if (dayOfMonth is < 1 or > 31)
			return Result<RecurringTransaction, DomainException>.Failure(error: new InvalidDayOfMonthException(message: "Day of month must be between 1 and 31."));

		return Result<RecurringTransaction, DomainException>.Success(value: new RecurringTransaction
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			AccountId = accountId,
			CategoryId = categoryId,
			Amount = amount,
			Direction = direction,
			DayOfMonth = dayOfMonth,
			Description = description,
			IsActive = true,
			LastExecutedAt = null,
			LastMissedAt = null,
			RowVersion = 0,
			CreatedAt = createdAt
		});
	}

	public static RecurringTransaction Reconstitute(
		Guid id,
		Guid userId,
		Guid accountId,
		Guid categoryId,
		Money amount,
		DirectionType direction,
		int dayOfMonth,
		string? description,
		bool isActive,
		DateTimeOffset? lastExecutedAt,
		DateTimeOffset? lastMissedAt,
		int rowVersion,
		DateTimeOffset createdAt)
	{
		return new RecurringTransaction
		{
			Id = id,
			UserId = userId,
			AccountId = accountId,
			CategoryId = categoryId,
			Amount = amount,
			Direction = direction,
			DayOfMonth = dayOfMonth,
			Description = description,
			IsActive = isActive,
			LastExecutedAt = lastExecutedAt,
			LastMissedAt = lastMissedAt,
			RowVersion = rowVersion,
			CreatedAt = createdAt
		};
	}

	public Result<bool, DomainException> Activate()
	{
		if (IsActive)
			return Result<bool, DomainException>.Success(value: false);

		IsActive = true;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Deactivate()
	{
		if (!IsActive)
			return Result<bool, DomainException>.Success(value: false);

		IsActive = false;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeAmount(decimal amount)
	{
		if (!IsActive)
		{
			return Result<bool, DomainException>.Failure(error: new InactiveRecurringTransactionException(
				message: "Cannot change amount of an inactive recurring transaction."
			));
		}

		Result<Money, DomainException> money = Money.Positive(amount: amount, currency: Amount.Currency);
		if (money.IsFailure)
			return Result<bool, DomainException>.Failure(error: money.Error!);

		if (Amount == money.Value)
			return Result<bool, DomainException>.Success(value: false);

		Amount = money.Value;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeCurrency(Currency currency)
	{
		if (!IsActive)
		{
			return Result<bool, DomainException>.Failure(error: new InactiveRecurringTransactionException(
				message: "Cannot change currency of an inactive recurring transaction."
			));
		}

		if (Amount.Currency == currency)
			return Result<bool, DomainException>.Success(value: false);

		Result<Money, DomainException> money = Money.Create(amount: Amount.Amount, currency: currency);
		if (money.IsFailure)
			return Result<bool, DomainException>.Failure(error: money.Error!);

		Amount = money.Value;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeDayOfMonth(int dayOfMonth)
	{
		if (!IsActive)
		{
			return Result<bool, DomainException>.Failure(error: new InactiveRecurringTransactionException(
				message: "Cannot change day of month of an inactive recurring transaction."
			));
		}

		if (dayOfMonth is < 1 or > 31)
			return Result<bool, DomainException>.Failure(error: new InvalidDayOfMonthException(message: "Day of month must be between 1 and 31."));

		if (DayOfMonth == dayOfMonth)
			return Result<bool, DomainException>.Success(value: false);

		DayOfMonth = dayOfMonth;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<Unit, DomainException> MarkExecuted(DateTimeOffset executedAt)
	{
		if (!IsActive)
			return Result<Unit, DomainException>.Failure(error: new InactiveRecurringTransactionException(message: "Cannot execute an inactive recurring transaction."));

		LastExecutedAt = executedAt;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> MarkMissed(DateTimeOffset missedAt)
	{
		if (!IsActive)
			return Result<Unit, DomainException>.Failure(error: new InactiveRecurringTransactionException(message: "Cannot mark an inactive recurring transaction as missed."));

		LastMissedAt = missedAt;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
