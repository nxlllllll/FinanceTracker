using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
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
    public DateTimeOffset? LastExecutedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private RecurringTransaction() { }

    public static Result<RecurringTransaction, DomainException> Create(
        DateTimeOffset createdAt,
        Guid userId,
        Guid accountId,
        Guid categoryId,
        Money amount,
        DirectionType direction,
        int dayOfMonth,
        string? description)
    {
        if (dayOfMonth is < 1 or > 31)
            return Result<RecurringTransaction, DomainException>.Failure(error: new InvalidDayOfMonthException(message: "Day of month must be between 1 and 31."));
 
        return Result<RecurringTransaction, DomainException>.Success(value: new RecurringTransaction
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
            Direction = direction,
            DayOfMonth = dayOfMonth,
            Description = description,
            IsActive = true,
            LastExecutedAt = null,
            CreatedAt = createdAt
        });
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
        DateTimeOffset? lastExecutedAt,
        DateTimeOffset createdAt)
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

    public Result<Unit, DomainException> Activate()
    {
        if (IsActive)
            return Result<Unit, DomainException>.Failure(error: new ActivatingException(message: "Recurring transaction is already active."));
 
        IsActive = true;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> Deactivate()
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is already inactive."));
 
        IsActive = false;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> ChangeAmount(decimal amount)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));

        Result<Money, DomainException> money = Money.Create(amount: amount, currency: Amount.Currency);
        if (money.IsFailure)
            return Result<Unit, DomainException>.Failure(error: money.Error!);
 
        Amount = money.Value;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> ChangeCurrency(Currency currency)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));

        Result<Money, DomainException> money = Money.Create(amount: Amount.Amount, currency: currency);
        if (money.IsFailure)
            return Result<Unit, DomainException>.Failure(error: money.Error!);
 
        Amount = money.Value;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> ChangeDayOfMonth(int dayOfMonth)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));

        if (dayOfMonth is < 1 or > 31)
            return Result<Unit, DomainException>.Failure(error: new InvalidDayOfMonthException(message: "Day of month must be between 1 and 31."));
 
        DayOfMonth = dayOfMonth;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
    
    public Result<Unit, DomainException> MarkExecuted(DateTimeOffset executedAt)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));
 
        LastExecutedAt = executedAt;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
}
