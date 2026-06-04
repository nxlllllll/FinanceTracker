using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Transfer;

public sealed class Transfer
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }
    public Money AmountFrom { get; private set; }
    public Money AmountTo { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public bool IsRatePending { get; private set; }
	public TransferStatus Status { get; private set; }
    public string? Description { get; private set; }
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
			OccurredAt = occurredAt
		});
	}
}