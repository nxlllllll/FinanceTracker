using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.RecurringTransaction;

/// <summary>
/// Represents a monthly recurring transaction that is automatically triggered
/// on a specific day of month by <c>RecurringTransactionHandlingJob</c>.
/// </summary>
public sealed class RecurringTransaction
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AccountId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Money Amount { get; private set; }
    public DirectionType Direction { get; private set; }
    /// <summary>Day of month (1–31) on which this transaction is triggered. Days exceeding the month length execute on the last day.</summary>
    public int DayOfMonth { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    /// <summary>UTC timestamp of the last successful execution. <c>null</c> if never executed.</summary>
    public DateTimeOffset? LastExecutedAt { get; private set; }
    public int RowVersion { get; private set; }
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
            RowVersion = 0,
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
        int rowVersion,
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
            RowVersion = rowVersion,
            CreatedAt = createdAt
        };
    }

    public Result<Unit, DomainException> Activate()
    {
        if (IsActive)
            return Result<Unit, DomainException>.Failure(error: new ActivatingException(message: "Recurring transaction is already active."));
 
        IsActive = true;
        ++RowVersion;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> Deactivate()
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is already inactive."));
 
        IsActive = false;
        ++RowVersion;
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
        ++RowVersion;
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
        ++RowVersion;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
 
    public Result<Unit, DomainException> ChangeDayOfMonth(int dayOfMonth)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));

        if (dayOfMonth is < 1 or > 31)
            return Result<Unit, DomainException>.Failure(error: new InvalidDayOfMonthException(message: "Day of month must be between 1 and 31."));
 
        DayOfMonth = dayOfMonth;
        ++RowVersion;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
    
    public Result<Unit, DomainException> MarkExecuted(DateTimeOffset executedAt)
    {
        if (!IsActive)
            return Result<Unit, DomainException>.Failure(error: new DeactivatingException(message: "Recurring transaction is inactive."));
 
        LastExecutedAt = executedAt;
        ++RowVersion;
        return Result<Unit, DomainException>.Success(value: Unit.Default);
    }
}