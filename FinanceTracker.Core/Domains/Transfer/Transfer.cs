namespace FinanceTracker.Core.Domains.Transfer;

public sealed class Transfer
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FromAccountId { get; private set; }
    public Guid ToAccountId { get; private set; }
    public decimal AmountFrom { get; private set; }
    public string CurrencyFrom { get; private set; } = String.Empty;
    public decimal AmountTo { get; private set; }
    public string CurrencyTo { get; private set; } = String.Empty;
    public decimal ExchangeRate { get; private set; }
    public bool IsExcluded { get; private set; }
    public bool IsRatePending { get; private set; }
    public string? Description { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private Transfer() { }

    public static Transfer Create(
        Guid userId,
        Guid fromAccountId,
        Guid toAccountId,
        decimal amountFrom,
        string currencyFrom,
        decimal amountTo,
        string currencyTo,
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
            AmountFrom = amountFrom,
            CurrencyFrom = currencyFrom,
            AmountTo = amountTo,
            CurrencyTo = currencyTo,
            ExchangeRate = exchangeRate,
            IsExcluded = false,
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
        string currencyFrom,
        decimal amountTo,
        string currencyTo,
        decimal exchangeRate,
        bool isExcluded,
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
            AmountFrom = amountFrom,
            CurrencyFrom = currencyFrom,
            AmountTo = amountTo,
            CurrencyTo = currencyTo,
            ExchangeRate = exchangeRate,
            IsExcluded = isExcluded,
            IsRatePending = isRatePending,
            Description = description,
            OccurredAt = occurredAt
        };
    }
}