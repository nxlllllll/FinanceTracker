using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Transaction;

public sealed class Transaction
{
    public Guid Id { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Money Amount { get; private set; }
    public DirectionType Direction { get; private set; }
    public decimal ExchangeRate { get; private set; }
    public bool IsExcluded { get; private set; }
    public bool IsRatePending { get; private set; }
    public string? Description { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private Transaction() { }

    public static Transaction Create(
		DateTime occurredAt,
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
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            Direction = direction,
            ExchangeRate = exchangeRate,
            IsExcluded = false,
            IsRatePending = isRatePending,
            Description = description,
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
        DateTime occurredAt)
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
            OccurredAt = occurredAt
        };
    }

    public void Exclude()
    {
        if (IsExcluded)
            throw new ExcludingException("Transaction is already excluded.");
        
        IsExcluded = true;
    }

    public void Include()
    {
        if (!IsExcluded)
            throw new IncludingException("Transaction is not excluded.");
        
        IsExcluded = false;
    }

    public void ChangeCategory(Guid categoryId)
        => CategoryId = categoryId;

    public void ChangeDescription(string? description)
        => Description = description;
}