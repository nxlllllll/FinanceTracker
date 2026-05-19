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
    public string? Description { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private Transfer() { }

    public static Result<Transfer, DomainException> Create(
        Guid userId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amountFrom,
        Currency currencyFrom,
        decimal amountTo,
        Currency currencyTo,
        decimal exchangeRate,
        bool isRatePending,
        string? description,
        DateTime occurredAt)
    {
        Result<Money, DomainException> amountFromResult = Money.Create(amount: amountFrom, currency: currencyFrom);
        if (amountFromResult.IsFailure)
            return Result<Transfer, DomainException>.Failure(error: amountFromResult.Error!);

        Result<Money, DomainException> amountToResult = Money.Create(amount: amountTo, currency: currencyTo);
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
            Description = description,
            OccurredAt = occurredAt
        });
    }

    public static Transfer Reconstitute(
        Guid id,
        Guid userId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amountFrom,
        Currency currencyFrom,
        decimal amountTo,
        Currency currencyTo,
        decimal exchangeRate,
        bool isRatePending,
        string? description,
        DateTime occurredAt)
    {
        return new Transfer
        {
            Id = id,
            UserId = userId,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            AmountFrom = Money.Reconstitute(amount: amountFrom, currency: currencyFrom),
            AmountTo = Money.Reconstitute(amount: amountTo, currency: currencyTo),
            ExchangeRate = exchangeRate,
            IsRatePending = isRatePending,
            Description = description,
            OccurredAt = occurredAt
        };
    }
}