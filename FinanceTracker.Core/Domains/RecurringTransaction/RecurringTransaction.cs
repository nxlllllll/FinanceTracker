using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

public sealed class RecurringTransaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = String.Empty;
    public DirectionType Direction { get; private set; }
    public int DayOfMonth { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RecurringTransaction() { }

    public static RecurringTransaction Create(
        Guid userId,
        Guid accountId,
        Guid categoryId,
        decimal amount,
        string currency,
        DirectionType direction,
        int dayOfMonth,
        string? description)
    {
        if (amount <= 0)
            throw new InvalidAmountException("Amount must be greater than zero.");

        if (dayOfMonth is < 1 or > 31)
            throw new InvalidDayOfMonthException("Day of month must be between 1 and 31.");

        return new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
            Currency = currency,
            Direction = direction,
            DayOfMonth = dayOfMonth,
            Description = description,
            IsActive = true,
            LastExecutedAt = null,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static RecurringTransaction Reconstitute(
        Guid id,
        Guid userId,
        Guid accountId,
        Guid categoryId,
        decimal amount,
        string currency,
        DirectionType direction,
        int dayOfMonth,
        string? description,
        bool isActive,
        DateTime? lastExecutedAt,
        DateTime createdAt)
    {
        return new RecurringTransaction
        {
            Id = id,
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
            Currency = currency,
            Direction = direction,
            DayOfMonth = dayOfMonth,
            Description = description,
            IsActive = isActive,
            LastExecutedAt = lastExecutedAt,
            CreatedAt = createdAt
        };
    }

    public void Activate()
    {
        if (IsActive)
            throw new ActivatingException("Recurring transaction is already active.");
        
        IsActive = true;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new DeactivatingException("Recurring transaction is already inactive.");

        IsActive = false;
    }

    public void ChangeAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidAmountException("Amount must be greater than zero.");
        
        if (Amount == amount)
            return;
        
        Amount = amount;
    }

    public void ChangeCurrency(string currency)
    {
        if (Currency == currency)
            return;
        
        Currency = currency;
    }

    public void ChangeDayOfMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
            throw new InvalidDayOfMonthException("Day of month must be between 1 and 31.");

        if (DayOfMonth == dayOfMonth)
            return;
        
        DayOfMonth = dayOfMonth;
    }
}