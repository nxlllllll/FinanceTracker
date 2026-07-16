namespace FinanceTracker.Core.Repositories.Budget;

public interface IBudgetWriteRepository
{
	Task CreateAsync(
		Domains.Budget.Budget budget,
		CancellationToken ct = default
	);

	Task ChangeAmountAsync(
		Guid budgetId,
		decimal amount,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ChangePeriodAsync(
		Guid budgetId,
		DateOnly from,
		DateOnly to,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task ActivateAsync(
		Guid budgetId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task DeactivateAsync(
		Guid budgetId,
		int expectedVersion,
		CancellationToken ct = default
	);

	Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default
	);
}
