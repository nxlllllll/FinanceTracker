namespace FinanceTracker.Core.Repositories.CategoryTotals;

public interface ICategoryTotalWriteRepository
{
	Task AddAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default);
 
	Task SubtractAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default);
 
	Task ChangeCategoryAsync(
		Guid userId,
		Guid oldCategoryId,
		Guid newCategoryId,
		decimal amount,
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default);
}
