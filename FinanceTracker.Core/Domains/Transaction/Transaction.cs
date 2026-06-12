using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Transaction;

/// <summary>
/// Represents a financial transaction (debit or credit) on an account.
/// When <see cref="IsRatePending"/> is <c>true</c>, the exchange rate was not available
/// at creation time and will be updated by <c>BalanceAdjustmentJob</c>.
/// </summary>
public sealed class Transaction
{
	public Guid Id { get; private set; }
	public Guid AccountId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid CategoryId { get; private set; }
	public Money Amount { get; private set; }
	public DirectionType Direction { get; private set; }
	/// <summary>Exchange rate applied when the account currency differs from the transaction currency.</summary>
	public decimal ExchangeRate { get; private set; }
	/// <summary>When <c>true</c>, this transaction is excluded from budget and total calculations.</summary>
	public bool IsExcluded { get; private set; }
	/// <summary>When <c>true</c>, the exchange rate is a placeholder and will be updated by <c>BalanceAdjustmentJob</c>.</summary>
	public bool IsRatePending { get; private set; }
	public string? Description { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }

	private Transaction() { }

	public static Transaction Create(
		DateTimeOffset occurredAt,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		Money amount,
		DirectionType direction,
		decimal exchangeRate,
		bool isRatePending,
		string? description)
	{
		return new Transaction()
		{
			Id = Guid.CreateVersion7(),
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Direction = direction,
			ExchangeRate = exchangeRate,
			IsExcluded = false,
			IsRatePending = isRatePending,
			Description = description,
			RowVersion = 0,
			OccurredAt = occurredAt
		};
	}

	public static Transaction Reconstitute(
		Guid id,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		Money amount,
		DirectionType direction,
		decimal exchangeRate,
		bool isExcluded,
		bool isRatePending,
		string? description,
		int rowVersion,
		DateTimeOffset occurredAt)
	{
		return new Transaction()
		{
			Id = id,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			Direction = direction,
			ExchangeRate = exchangeRate,
			IsExcluded = isExcluded,
			IsRatePending = isRatePending,
			Description = description,
			RowVersion = rowVersion,
			OccurredAt = occurredAt
		};
	}

    public Result<Unit, DomainException> Exclude()
	{
		if (IsExcluded)
			return Result<Unit, DomainException>.Failure(error: new ExcludingException(message: "Transaction is already excluded."));

		IsExcluded = true;
		RowVersion++;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> Include()
	{
		if (!IsExcluded)
			return Result<Unit, DomainException>.Failure(error: new IncludingException(message: "Transaction is not excluded."));

		IsExcluded = false;
		RowVersion++;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

    public Result<Unit, DomainException> ChangeCategory(Guid categoryId)
	{
		if (IsExcluded)
			return Result<Unit, DomainException>.Failure(error: new ExcludedOperationException(message: "Cannot modify an excluded transaction."));

		CategoryId = categoryId;
		RowVersion++;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ChangeDescription(string? description)
	{
		if (IsExcluded)
			return Result<Unit, DomainException>.Failure(error: new ExcludedOperationException(message: "Cannot modify an excluded transaction."));
		
		Description = description;
		RowVersion++;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}