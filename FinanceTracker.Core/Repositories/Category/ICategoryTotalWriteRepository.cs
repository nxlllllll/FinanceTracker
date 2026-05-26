namespace FinanceTracker.Core.Repositories.Category;

public interface ICategoryTotalWriteRepository
{
	Task AddAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
		CancellationToken ct = default);
 
	Task SubtractAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
		CancellationToken ct = default);
 
	Task ChangeCategoryAsync(
		Guid userId,
		Guid oldCategoryId,
		Guid newCategoryId,
		decimal amount,
		ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
		CancellationToken ct = default);
}
