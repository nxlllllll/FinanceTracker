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
    public bool IsActive { get; private set; }
    public DateOnly From { get; private set; }
    public DateOnly To { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Budget() { }

    public static Result<Budget, DomainException> Create(
        DateTimeOffset createdAt,
        Guid userId,
        Guid categoryId,
        Money amount,
        DateOnly from,
        DateOnly to)
    {
        if (to <= from)
            return Result<Budget, DomainException>.Failure(error: new InvalidBudgetPeriodException(message: "Budget end date must be after start date."));

        return Result<Budget, DomainException>.Success(value: new Budget
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            IsActive = true,
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
        bool isActive,
        DateOnly from,
        DateOnly to,
        DateTimeOffset createdAt)
    {
        return new Budget
        {
            Id = id,
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            IsActive = isActive,
            From = from,
            To = to,
            CreatedAt = createdAt
        };
    }

    public Result<Unit, DomainException> ChangeAmount(decimal amount)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new InactiveBudgetException(message: "Cannot change amount of an inactive budget."));

        Result<Money, DomainException> money = Money.Positive(amount: amount, currency: Amount.Currency);
        if (money.IsFailure)
            return Result<Unit, DomainException>.Failure(error: money.Error!);

        Amount = money.Value!;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }

    public Result<Unit, DomainException> ChangePeriod(DateOnly from, DateOnly to)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new InactiveBudgetException(message: "Cannot change period of an inactive budget."));

        if (to <= from)
            return Result<Unit, DomainException>.Failure(error: new InvalidBudgetPeriodException(message: "Budget end date must be after start date."));

        From = from;
        To = to;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }

    public Result<Unit, DomainException> Activate()
    {
        if (IsActive)
            return Result<Unit, DomainException>.Failure(error: new ActivatingException(message: "Budget is already active."));

        IsActive = true;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }

    public Result<Unit, DomainException> Deactivate()
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Budget is already inactive."));

        IsActive = false;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
}