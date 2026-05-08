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

    public static Transfer Create(
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
            Id = Guid.NewGuid(),
            UserId = userId,
            FromAccountId = fromAccountId,
            ToAccountId = toAccountId,
            AmountFrom = Money.Create(amount: amountFrom, currency: currencyFrom).Value,
            AmountTo = Money.Create(amount: amountTo, currency: currencyTo).Value,
            ExchangeRate = exchangeRate,
            IsRatePending = isRatePending,
            Description = description,
            OccurredAt = occurredAt
        };
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
            AmountFrom = Money.Create(amount: amountFrom, currency: Currency.Create(value: currencyFrom).Value).Value,
            AmountTo = Money.Create(amount: amountTo, currency: Currency.Create(value: currencyTo).Value).Value,
            ExchangeRate = exchangeRate,
            IsRatePending = isRatePending,
            Description = description,
            OccurredAt = occurredAt
        };
    }
}