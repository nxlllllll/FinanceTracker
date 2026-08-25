using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Rate;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Transaction;

/// <summary>
/// Represents a financial transaction (debit or credit) on an account.
/// <para>
/// The exchange rate carries a lifecycle of its own — see <see cref="RateStatus"/>. While it sits
/// in <see cref="Abstractions.Rate.RateStatus.Pending"/>, the stored <see cref="ExchangeRate"/> is a placeholder and
/// <c>BalanceAdjustmentJob</c> will replace it with the real rate and post the difference to the
/// account balance. Every other state is terminal: the rate question is settled and the row will
/// never be picked up again.
/// </para>
/// </summary>
public sealed class Transaction : IHasId
{
	public Guid Id { get; private set; }
	public Guid AccountId { get; private set; }
	public Guid UserId { get; private set; }
	public Guid CategoryId { get; private set; }
	public Money Amount { get; private set; }
	public Currency BaseCurrency { get; private set; }
	public DirectionType Direction { get; private set; }
	public decimal ExchangeRate { get; private set; }
	public RateStatus RateStatus { get; private set; }
	public DateTimeOffset RateStatusChangedAt { get; private set; }
	public bool IsExcluded { get; private set; }
	public bool IsCancelled { get; private set; }
	public DateTimeOffset? CancelledAt { get; private set; }
	public string? Description { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }

	private Transaction() { }

	/// <summary>
	/// Creates a new transaction. Fails if <paramref name="exchangeRate"/> is not positive, or if
	/// <paramref name="rateStatus"/> is not a state an operation can legitimately start in.
	/// <paramref name="amount"/> must already be a validated <see cref="Money"/> value.
	/// </summary>
	public static Result<Transaction, DomainException> Create(
		DateTimeOffset createdAt,
		DateTimeOffset occurredAt,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		Money amount,
		Currency baseCurrency,
		DirectionType direction,
		decimal exchangeRate,
		RateStatus rateStatus,
		string? description)
	{
		if (exchangeRate <= 0)
			return Result<Transaction, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		if (rateStatus is not (RateStatus.Exact or RateStatus.Pending or RateStatus.Approximated))
		{
			return Result<Transaction, DomainException>.Failure(error: new InvalidRateStatusTransitionException(
				message: $"A transaction cannot be created directly in {rateStatus}. Valid initial states: {RateStatus.Exact}, {RateStatus.Pending}, {RateStatus.Approximated}.",
				from: RateStatus.Exact,
				to: rateStatus
			));
		}

		return Result<Transaction, DomainException>.Success(value: new Transaction
		{
			Id = Guid.CreateVersion7(),
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			BaseCurrency = baseCurrency,
			Direction = direction,
			ExchangeRate = exchangeRate,
			RateStatus = rateStatus,
			RateStatusChangedAt = createdAt,
			IsExcluded = false,
			IsCancelled = false,
			CancelledAt = null,
			Description = description,
			RowVersion = 0,
			CreatedAt = createdAt,
			OccurredAt = occurredAt
		});
	}

	/// <summary>Bypasses validation. Use only when rehydrating from storage.</summary>
	public static Transaction Reconstitute(
		Guid id,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		Money amount,
		Currency baseCurrency,
		DirectionType direction,
		decimal exchangeRate,
		RateStatus rateStatus,
		DateTimeOffset rateStatusChangedAt,
		bool isExcluded,
		bool isCancelled,
		DateTimeOffset? cancelledAt,
		string? description,
		int rowVersion,
		DateTimeOffset createdAt,
		DateTimeOffset occurredAt)
	{
		return new Transaction
		{
			Id = id,
			AccountId = accountId,
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			BaseCurrency = baseCurrency,
			Direction = direction,
			ExchangeRate = exchangeRate,
			RateStatus = rateStatus,
			RateStatusChangedAt = rateStatusChangedAt,
			IsExcluded = isExcluded,
			IsCancelled = isCancelled,
			CancelledAt = cancelledAt,
			Description = description,
			RowVersion = rowVersion,
			CreatedAt = createdAt,
			OccurredAt = occurredAt
		};
	}

	public Result<Unit, DomainException> Cancel(DateTimeOffset cancelledAt, TimeSpan maxAge)
	{
		if (IsCancelled)
			return Result<Unit, DomainException>.Failure(error: new CancelledOperationException(message: "Transaction has already been cancelled."));

		if (cancelledAt - CreatedAt > maxAge)
		{
			return Result<Unit, DomainException>.Failure(error: new TransactionCancellationWindowExpiredException(
				message: $"Transaction can no longer be cancelled: it was recorded on {CreatedAt:yyyy-MM-dd} and the cancellation window is {maxAge.TotalDays:0} day(s)."
			));
		}

		IsCancelled = true;
		CancelledAt = cancelledAt;
		CancelPendingRate(occurredAt: cancelledAt);

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ResolveRate(decimal newRate, DateTimeOffset changedAt)
	{
		if (newRate <= 0)
			return Result<Unit, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		Result<RateStatus, DomainException> transition = RateStatusTransitions.To(from: RateStatus, to: RateStatus.Resolved);
		if (transition.IsFailure)
			return Result<Unit, DomainException>.Failure(error: transition.Error!);

		ExchangeRate = newRate;
		RateStatus = transition.Value;
		RateStatusChangedAt = changedAt;

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ApproximateRate(DateTimeOffset changedAt)
	{
		Result<RateStatus, DomainException> transition = RateStatusTransitions.To(from: RateStatus, to: RateStatus.Approximated);
		if (transition.IsFailure)
			return Result<Unit, DomainException>.Failure(error: transition.Error!);

		RateStatus = transition.Value;
		RateStatusChangedAt = changedAt;

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> MarkRateUnresolvable(DateTimeOffset changedAt)
	{
		Result<RateStatus, DomainException> transition = RateStatusTransitions.To(from: RateStatus, to: RateStatus.Unresolvable);
		if (transition.IsFailure)
			return Result<Unit, DomainException>.Failure(error: transition.Error!);

		RateStatus = transition.Value;
		RateStatusChangedAt = changedAt;

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<bool, DomainException> Exclude()
	{
		if (IsCancelled)
			return Result<bool, DomainException>.Failure(error: new CancelledOperationException(message: "Cannot modify a cancelled transaction."));

		if (IsExcluded)
			return Result<bool, DomainException>.Success(value: false);

		IsExcluded = true;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Include()
	{
		if (IsCancelled)
			return Result<bool, DomainException>.Failure(error: new CancelledOperationException(message: "Cannot modify a cancelled transaction."));

		if (!IsExcluded)
			return Result<bool, DomainException>.Success(value: false);

		IsExcluded = false;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeCategory(Guid categoryId)
	{
		if (IsCancelled)
			return Result<bool, DomainException>.Failure(error: new CancelledOperationException(message: "Cannot modify a cancelled transaction."));

		if (IsExcluded)
			return Result<bool, DomainException>.Failure(error: new ExcludedOperationException(message: "Cannot modify an excluded transaction."));

		if (CategoryId == categoryId)
			return Result<bool, DomainException>.Success(value: false);

		CategoryId = categoryId;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeDescription(string? description)
	{
		if (IsCancelled)
			return Result<bool, DomainException>.Failure(error: new CancelledOperationException(message: "Cannot modify a cancelled transaction."));

		if (IsExcluded)
			return Result<bool, DomainException>.Failure(error: new ExcludedOperationException(message: "Cannot modify an excluded transaction."));

		if (Description == description)
			return Result<bool, DomainException>.Success(value: false);

		Description = description;
		return Result<bool, DomainException>.Success(value: true);
	}

	private void CancelPendingRate(DateTimeOffset occurredAt)
	{
		if (!RateStatus.IsOpen())
			return;

		RateStatus = RateStatus.Cancelled;
		RateStatusChangedAt = occurredAt;
	}
}
