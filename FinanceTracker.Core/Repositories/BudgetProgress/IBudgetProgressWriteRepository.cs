namespace FinanceTracker.Core.Repositories.BudgetProgress;

public interface IBudgetProgressWriteRepository
{
	Task AddAsync(
		Guid userId,
		Guid categoryId,
		string currencyCode,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default
	);

	Task SubtractAsync(
		Guid userId,
		Guid categoryId,
		string currencyCode,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default
	);
	
	Task ChangeCategoryAsync(
		Guid userId,
		Guid oldCategoryId,
		Guid newCategoryId,
		string currencyCode,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default
	);
}