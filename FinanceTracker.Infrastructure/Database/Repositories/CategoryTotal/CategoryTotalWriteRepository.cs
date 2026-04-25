using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;

public sealed class CategoryTotalWriteRepository(
	FinanceTrackerContext context
) : ICategoryTotalWriteRepository
{
	private async Task ApplyDeltaAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		int delta,
		DateTime occurredAt,
		CancellationToken ct)
	{
		DateOnly period = new DateOnly(year: occurredAt.Year, month: occurredAt.Month, day: 1);

		CategoryTotalEntity? existing = await context.CategoryTotals.FirstOrDefaultAsync(
			predicate: total => total.UserId == userId && total.CategoryId == categoryId && total.Period == period,
			cancellationToken: ct
		);

		if (existing is null)
		{
			await context.CategoryTotals.AddAsync(entity: new CategoryTotalEntity
			{
				Id = Guid.NewGuid(),
				UserId = userId,
				CategoryId = categoryId,
				Period = period,
				Total = amount * delta,
				TransactionCount = delta,
				UpdatedAt = DateTime.UtcNow
			}, cancellationToken: ct);
		}
		else
		{
			existing.Total += amount * delta;
			existing.TransactionCount += delta;
			existing.UpdatedAt = DateTime.UtcNow;
		}

		await context.SaveChangesAsync(cancellationToken: ct);
	}
	
	public async Task AddAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			amount: amount,
			delta: 1,
			occurredAt: occurredAt,
			ct: ct
		);
	}

	public async Task SubtractAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			amount: amount,
			delta: -1,
			occurredAt: occurredAt,
			ct: ct
		);
	}

	public async Task ChangeCategoryAsync(
		Guid userId,
		Guid oldCategoryId,
		Guid newCategoryId,
		decimal amount,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: oldCategoryId,
			amount: amount,
			delta: -1,
			occurredAt: occurredAt,
			ct: ct
		);
		
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: newCategoryId,
			amount: amount,
			delta: 1,
			occurredAt: occurredAt,
			ct: ct
		);
	}
}