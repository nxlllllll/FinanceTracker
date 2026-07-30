using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Budget;

/// <summary>
/// Represents a spending budget for a specific category over a date range.
/// A budget is active when today falls within [<see cref="From"/>, <see cref="To"/>].
/// Progress is tracked separately in <c>rm_budget_progress</c>.
/// </summary>
public sealed class Budget
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public Guid CategoryId { get; private set; }
	/// <summary>The budget spending limit.</summary>
	public Money Amount { get; private set; }
	public bool IsActive { get; private set; }
	/// <summary>Inclusive start date of the budget period.</summary>
	public DateOnly From { get; private set; }
	/// <summary>Inclusive end date of the budget period.</summary>
	public DateOnly To { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

	private Budget() { }

	/// <summary>
	/// Creates a new budget. Fails if <paramref name="to"/> is before <paramref name="from"/>;
	/// </summary>
	public static Result<Budget, DomainException> Create(
		DateTimeOffset createdAt,
		Guid userId,
		Guid categoryId,
		Money amount,
		DateOnly from,
		DateOnly to)
	{
		if (to < from)
			return Result<Budget, DomainException>.Failure(error: new InvalidBudgetPeriodException(message: "Budget end date must not be before start date."));

		return Result<Budget, DomainException>.Success(value: new Budget
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			CategoryId = categoryId,
			Amount = amount,
			IsActive = true,
			From = from,
			To = to,
			RowVersion = 0,
			CreatedAt = createdAt
		});
	}

	/// <summary>Bypasses validation. Use only when loading from storage.</summary>
	public static Budget Reconstitute(
		Guid id,
		Guid userId,
		Guid categoryId,
		Money amount,
		bool isActive,
		DateOnly from,
		DateOnly to,
		int rowVersion,
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
			RowVersion = rowVersion,
			CreatedAt = createdAt
		};
	}

	public Result<bool, DomainException> ChangeAmount(decimal amount)
	{
		if (!IsActive)
			return Result<bool, DomainException>.Failure(error: new InactiveBudgetException(message: "Cannot change amount of an inactive budget."));

		Result<Money, DomainException> money = Money.Positive(amount: amount, currency: Amount.Currency);
		if (money.IsFailure)
			return Result<bool, DomainException>.Failure(error: money.Error!);

		if (Amount == money.Value!)
			return Result<bool, DomainException>.Success(value: false);

		Amount = money.Value!;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangePeriod(DateOnly from, DateOnly to)
	{
		if (!IsActive)
			return Result<bool, DomainException>.Failure(error: new InactiveBudgetException(message: "Cannot change period of an inactive budget."));

		if (to < from)
			return Result<bool, DomainException>.Failure(error: new InvalidBudgetPeriodException(message: "Budget end date must not be before start date."));

		if (From == from && To == to)
			return Result<bool, DomainException>.Success(value: false);

		From = from;
		To = to;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Activate()
	{
		if (IsActive)
			return Result<bool, DomainException>.Success(value: false);

		IsActive = true;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Deactivate()
	{
		if (!IsActive)
			return Result<bool, DomainException>.Success(value: false);

		IsActive = false;
		return Result<bool, DomainException>.Success(value: true);
	}
}
