using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Budget;

public sealed class Budget
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Money Amount { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Budget() { }

    public static Budget Create(
		DateTime createdAt,
        Guid userId,
        Guid categoryId,
        Money amount,
        DateOnly from,
        DateOnly to)
    {
        if (to <= from)
            throw new InvalidBudgetPeriodException("Budget end date must be after start date.");

        return new Budget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            From = from,
            To = to,
            CreatedAt = createdAt
        };
    }

    public static Budget Reconstitute(
        Guid id,
        Guid userId,
        Guid categoryId,
        Money amount,
        DateOnly from,
        DateOnly to,
        DateTime createdAt)
    {
        return new Budget
        {
            Id = id,
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            From = from,
            To = to,
            CreatedAt = createdAt
        };
    }

    public void ChangeAmount(decimal amount)
        => Amount = Money.Positive(amount: amount, currency: Amount.Currency);

    public void ChangePeriod(DateOnly from, DateOnly to)
    {
        if (to <= from)
            throw new InvalidBudgetPeriodException(message: "Budget end date must be after start date.");
        
        From = from;
        To = to;
    }
}