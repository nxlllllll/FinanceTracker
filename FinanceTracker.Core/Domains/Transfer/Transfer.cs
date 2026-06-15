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
	/// <summary>Amount debited from the source account (in source currency).</summary>
	public Money AmountFrom { get; private set; }
	/// <summary>Amount to credit to the destination account (in destination currency).</summary>
	public Money AmountTo { get; private set; }
	public decimal ExchangeRate { get; private set; }
	/// <summary>
	/// <c>true</c> when the exchange rate was not available at creation time
	/// and will be filled in by <c>BalanceAdjustmentJob</c>.
	/// </summary>
	public bool IsRatePending { get; private set; }
	public TransferStatus Status { get; private set; }
	public string? Description { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }

	private Transfer() { }

	public static Result<Transfer, DomainException> Create(
		Guid userId,
		Guid fromAccountId,
		Guid toAccountId,
		decimal amount,
		Currency currencyFrom,
		Currency currencyTo,
		decimal exchangeRate,
		bool isRatePending,
		string? description,
		DateTimeOffset occurredAt)
	{
		if (fromAccountId == toAccountId)
			return Result<Transfer, DomainException>.Failure(error: new SameAccountTransferException(message: "Cannot transfer to the same account."));

		if (exchangeRate <= 0)
			return Result<Transfer, DomainException>.Failure(error: new InvalidExchangeRateException(message: "Exchange rate must be greater than zero."));

		Result<Money, DomainException> amountFromResult = Money.Create(amount: amount, currency: currencyFrom);
		if (amountFromResult.IsFailure)
			return Result<Transfer, DomainException>.Failure(error: amountFromResult.Error!);

		Result<Money, DomainException> amountToResult = Money.Create(amount: amount * exchangeRate, currency: currencyTo);
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
			IsRatePending = isRatePending,
			Status = TransferStatus.PendingCredit,
			Description = description,
			RowVersion = 0,
			OccurredAt = occurredAt
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
		bool isRatePending,
		TransferStatus status,
		string? description,
		int rowVersion,
		DateTimeOffset occurredAt)
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
			IsRatePending = isRatePending,
			Status = status,
			Description = description,
			RowVersion = rowVersion,
			OccurredAt = occurredAt
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
	/// Marks the transfer as compensated — the debit was refunded to the source account
	/// because the credit side failed.
	/// </summary>
	public Result<Unit, DomainException> Compensate()
	{
		if (Status != TransferStatus.PendingCredit)
			return Result<Unit, DomainException>.Failure(error: new InvalidTransferStatusException(message: $"Transfer can only be compensated from PendingCredit state. Current state: {Status}."));

		Status = TransferStatus.Compensated;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>
	/// Marks the transfer as permanently failed — compensation itself failed and
	/// manual intervention is required.
	/// </summary>
	public Result<Unit, DomainException> Fail()
	{
		if (Status is TransferStatus.Completed or TransferStatus.Failed)
			return Result<Unit, DomainException>.Failure(error: new InvalidTransferStatusException(message: $"Transfer cannot be failed from {Status} state."));

		Status = TransferStatus.Failed;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}