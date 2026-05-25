using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Extensions;

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
		Core.ValueObjects.Currency currency,
		int delta,
		DateTimeOffset occurredAt,
		CancellationToken ct)
	{
		DateOnly date = DateOnly.FromDateTime(dateTime: occurredAt.UtcDateTime);
		DateOnly period = new DateOnly(year: occurredAt.Year, month: occurredAt.Month, day: 1);

		Core.Domains.User.User user = await userReadRepository.GetByIdAsync(userId: userId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: userId);
		
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: currency,
			toCurrency: user.BaseCurrency,
			date: date,
			ct: ct
		);
			
		await context.UpsertCategoryTotalAsync(entity: new CategoryTotalEntity
		{
		    Id = Guid.CreateVersion7(),
		    UserId = userId,
		    CategoryId = categoryId,
		    Period = period,
		    Total = amount * conversion.Rate * delta,
		    TransactionCount = delta,
		    UpdatedAt = dateProvider.UtcNow
		}, ct: ct);
	}
	
	public async Task AddAsync(
		Guid userId,
		Guid categoryId,
		decimal amount,
		Core.ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
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
		Core.ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
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
		Core.ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
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
