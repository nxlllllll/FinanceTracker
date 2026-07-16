using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Transfer;

/// <summary>
/// Represents a two-phase fund transfer between two accounts (debit → credit).
/// Created with status <c>PendingCredit</c>; transitions to <c>Completed</c>,
/// <c>Compensated</c>, or <c>Failed</c> via the transfer worker.
/// </summary>
public sealed class Transfer
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public Guid FromAccountId { get; private set; }
	public Guid ToAccountId { get; private set; }
	public Money AmountFrom { get; private set; }
	public Money AmountTo { get; private set; }
	public decimal ExchangeRate { get; private set; }
	public RateStatus RateStatus { get; private set; }
	public DateTimeOffset RateStatusChangedAt { get; private set; }
	public TransferStatus Status { get; private set; }
	public string? Description { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

	private Transfer() { }

	public static Result<Transfer, DomainException> Create(
		DateTimeOffset createdAt,
		Guid userId,
		Guid fromAccountId,
		Guid toAccountId,
		decimal amount,
		Currency currencyFrom,
		Currency currencyTo,
		decimal exchangeRate,
		RateStatus rateStatus,
		string? description,
		DateTimeOffset occurredAt)
	{
		if (fromAccountId == toAccountId)
			return Result<Transfer, DomainException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		if (exchangeRate <= 0)
			return Result<Transfer, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		if (rateStatus is not (RateStatus.Exact or RateStatus.Pending or RateStatus.Approximated))
		{
			return Result<Transfer, DomainException>.Failure(error: new InvalidRateStatusTransitionException(
				message: $"A transfer cannot be created directly in {rateStatus}. Valid initial states: {RateStatus.Exact}, {RateStatus.Pending}, {RateStatus.Approximated}.",
				from: RateStatus.Exact,
				to: rateStatus
			));
		}

		Result<Money, DomainException> amountFromResult = Money.Positive(amount: amount, currency: currencyFrom);
		if (amountFromResult.IsFailure)
			return Result<Transfer, DomainException>.Failure(error: amountFromResult.Error!);

		Result<Money, DomainException> amountToResult = Money.Positive(
			amount: Money.ConvertedAmount(amount: amount, rate: exchangeRate),
			currency: currencyTo
		);
		if (amountToResult.IsFailure)
			return Result<Transfer, DomainException>.Failure(error: amountToResult.Error!);

		return Result<Transfer, DomainException>.Success(value: new Transfer
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			FromAccountId = fromAccountId,
			ToAccountId = toAccountId,
			AmountFrom = amountFromResult.Value,
			AmountTo = amountToResult.Value,
			ExchangeRate = exchangeRate,
			RateStatus = rateStatus,
			RateStatusChangedAt = createdAt,
			Status = TransferStatus.PendingCredit,
			Description = description,
			RowVersion = 0,
			OccurredAt = occurredAt,
			CreatedAt = createdAt
		});
	}

	public static Transfer Reconstitute(
		Guid id,
		Guid userId,
		Guid fromAccountId,
		Guid toAccountId,
		Money amountFrom,
		Money amountTo,
		decimal exchangeRate,
		RateStatus rateStatus,
		DateTimeOffset rateStatusChangedAt,
		TransferStatus status,
		string? description,
		int rowVersion,
		DateTimeOffset occurredAt,
		DateTimeOffset createdAt)
	{
		return new Transfer
		{
			Id = id,
			UserId = userId,
			FromAccountId = fromAccountId,
			ToAccountId = toAccountId,
			AmountFrom = amountFrom,
			AmountTo = amountTo,
			ExchangeRate = exchangeRate,
			RateStatus = rateStatus,
			RateStatusChangedAt = rateStatusChangedAt,
			Status = status,
			Description = description,
			RowVersion = rowVersion,
			OccurredAt = occurredAt,
			CreatedAt = createdAt
		};
	}

	/// <summary>Marks the transfer as successfully completed (credit applied).</summary>
	public Result<Unit, DomainException> Complete()
	{
		if (Status != TransferStatus.PendingCredit)
			return Result<Unit, DomainException>.Failure(error: new InvalidTransferStatusException(message: $"Transfer can only be completed from PendingCredit state. Current state: {Status}."));

		Status = TransferStatus.Completed;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>
	/// Marks the transfer as compensated — the debit was refunded to the source account because the
	/// credit side failed. Also cancels any pending rate: the credit never landed, so there is no
	/// balance for a rate correction to correct.
	/// </summary>
	public Result<Unit, DomainException> Compensate(DateTimeOffset occurredAt)
	{
		if (Status != TransferStatus.PendingCredit)
			return Result<Unit, DomainException>.Failure(error: new InvalidTransferStatusException(message: $"Transfer can only be compensated from PendingCredit state. Current state: {Status}."));

		Status = TransferStatus.Compensated;
		CancelPendingRate(occurredAt: occurredAt);

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>
	/// Marks the transfer as permanently failed — compensation itself failed and manual intervention
	/// is required. Cancels any pending rate for the same reason as <see cref="Compensate"/>.
	/// </summary>
	public Result<Unit, DomainException> Fail(DateTimeOffset occurredAt)
	{
		if (Status is TransferStatus.Completed or TransferStatus.Failed)
			return Result<Unit, DomainException>.Failure(error: new InvalidTransferStatusException(message: $"Transfer cannot be failed from {Status} state."));

		Status = TransferStatus.Failed;
		CancelPendingRate(occurredAt: occurredAt);

		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	public Result<Unit, DomainException> ResolveRate(decimal newRate, DateTimeOffset changedAt)
	{
		if (newRate <= 0)
			return Result<Unit, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		Result<RateStatus, DomainException> transition = RateStatusTransitions.To(from: RateStatus, to: RateStatus.Resolved);
		if (transition.IsFailure)
			return Result<Unit, DomainException>.Failure(error: transition.Error!);

		Result<Money, DomainException> recomputed = Money.Positive(
			amount: Money.ConvertedAmount(amount: AmountFrom.Amount, rate: newRate),
			currency: AmountTo.Currency
		);
		if (recomputed.IsFailure)
			return Result<Unit, DomainException>.Failure(error: recomputed.Error!);

		ExchangeRate = newRate;
		AmountTo = recomputed.Value;
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

	private void CancelPendingRate(DateTimeOffset occurredAt)
	{
		if (!RateStatus.IsOpen())
			return;

		RateStatus = RateStatus.Cancelled;
		RateStatusChangedAt = occurredAt;
	}
}
