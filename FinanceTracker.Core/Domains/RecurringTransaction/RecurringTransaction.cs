using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

public sealed class RecurringTransaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Money Amount { get; private set; }
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
        Money amount,
        DirectionType direction,
        int dayOfMonth,
        string? description)
    {
        if (dayOfMonth is < 1 or > 31)
            throw new InvalidDayOfMonthException("Day of month must be between 1 and 31.");

        return new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
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
        Money amount,
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
        => Amount = new Money(amount: amount, currency: Amount.Currency);

    public void ChangeCurrency(string currency)
        => Amount = new Money(amount: Amount.Amount, currency: currency);

    public void ChangeDayOfMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > 31)
            throw new InvalidDayOfMonthException("Day of month must be between 1 and 31.");
        
        DayOfMonth = dayOfMonth;
    }
    
    public void MarkExecuted()
        => LastExecutedAt = DateTime.UtcNow;
}