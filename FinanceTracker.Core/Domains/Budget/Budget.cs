using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
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

    public static Result<Budget, DomainException> Create(
        DateTime createdAt,
        Guid userId,
        Guid categoryId,
        Money amount,
        DateOnly from,
        DateOnly to)
    {
        if (to <= from)
            return Result<Budget, DomainException>.Failure(error: new InvalidBudgetPeriodException("Budget end date must be after start date."));
 
        return Result<Budget, DomainException>.Success(value: new Budget
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            From = from,
            To = to,
            CreatedAt = createdAt
        });
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

    public Result<Unit, DomainException> ChangeAmount(decimal amount)
    {
        Result<Money, DomainException> money = Money.Positive(amount: amount, currency: Amount.Currency);
        if (money.IsFailure)
            return Result<Unit, DomainException>.Failure(error: money.Error!);
 
        Amount = money.Value!;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> ChangePeriod(DateOnly from, DateOnly to)
    {
        if (to <= from)
            return Result<Unit, DomainException>.Failure(error: new InvalidBudgetPeriodException(message: "Budget end date must be after start date."));
 
        From = from;
        To = to;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
}