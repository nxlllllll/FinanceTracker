using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;

public sealed class CategoryTotalWriteRepository(
	FinanceTrackerContext context,
	IUserReadRepository userReadRepository,
	ICurrencyConversionService currencyConversionService,
	IDateProvider dateProvider
) : ICategoryTotalWriteRepository
{
	private async Task ApplyDeltaAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		int delta,
		DateTime occurredAt,
		CancellationToken ct)
	{
		DateOnly date = DateOnly.FromDateTime(dateTime: occurredAt);
		DateOnly period = new DateOnly(year: occurredAt.Year, month: occurredAt.Month, day: 1);

		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: userId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: userId);
		
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: currency,
			toCurrency: user.BaseCurrency,
			date: date,
			ct: ct
		);
		
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
				Total = amount * conversion.Rate * delta,
				TransactionCount = delta,
				UpdatedAt = dateProvider.UtcNow
			}, cancellationToken: ct);
		}
		else
		{
			existing.Total += amount * conversion.Rate * delta;
			existing.TransactionCount += delta;
			existing.UpdatedAt = dateProvider.UtcNow;
		}

		await context.SaveChangesAsync(cancellationToken: ct);
	}
	
	public async Task AddAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			amount: amount,
			currency: currency,
			delta: 1,
			occurredAt: occurredAt,
			ct: ct
		);
	}

	public async Task SubtractAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			amount: amount,
			currency: currency,
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
		string currency,
		DateTime occurredAt,
		CancellationToken ct = default)
	{
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: oldCategoryId,
			currency: currency,
			amount: amount,
			delta: -1,
			occurredAt: occurredAt,
			ct: ct
		);
		
		await ApplyDeltaAsync(
			userId: userId,
			categoryId: newCategoryId,
			amount: amount,
			currency: currency,
			delta: 1,
			occurredAt: occurredAt,
			ct: ct
		);
	}
}