using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.Budget;

public sealed class Budget
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Currency { get; private set; } = String.Empty;
    public decimal Amount { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private Budget() { }

    public static Budget Create(
        Guid userId,
        Guid categoryId,
        string currency,
        decimal amount,
        DateOnly from,
        DateOnly to)
    {
        if (amount <= 0)
            throw new InvalidAmountException("Budget amount must be greater than zero.");

        if (to <= from)
            throw new InvalidBudgetPeriodException("Budget end date must be after start date.");

        return new Budget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = categoryId,
            Currency = currency,
            Amount = amount,
            From = from,
            To = to,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static Budget Reconstitute(
        Guid id,
        Guid userId,
        Guid categoryId,
        string currency,
        decimal amount,
        DateOnly from,
        DateOnly to,
        DateTime createdAt)
    {
        return new Budget
        {
            Id = id,
            UserId = userId,
            CategoryId = categoryId,
            Currency = currency,
            Amount = amount,
            From = from,
            To = to,
            CreatedAt = createdAt
        };
    }

    public void ChangeAmount(decimal amount)
    {
        if (amount <= 0)
            throw new InvalidAmountException(message: "Budget amount must be greater than zero.");

        if (Amount == amount)
            return;

        Amount = amount;
    }

    public void ChangePeriod(DateOnly from, DateOnly to)
    {
        if (to <= from)
            throw new InvalidBudgetPeriodException(message: "Budget end date must be after start date.");

        if (From == from && To == to)
            return;

        From = from;
        To = to;
    }
}