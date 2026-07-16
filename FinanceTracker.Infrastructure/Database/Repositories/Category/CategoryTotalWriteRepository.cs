using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryTotalWriteRepository(
	FinanceTrackerContext context,
	IUserQueryRepository userQueryRepository,
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
		DateOnly period = new DateOnly(year: occurredAt.Year, month: occurredAt.Month, day: 1);

		UserReadModel user = await userQueryRepository.GetByIdAsync(userId: userId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: userId);

		decimal rate = await currencyConversionService.GetStableRateAsync(
			fromCurrency: currency,
			toCurrency: user.BaseCurrency,
			asOf: occurredAt,
			ct: ct
		);

		await context.UpsertCategoryTotalAsync(entity: new CategoryTotalEntity
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			CategoryId = categoryId,
			Period = period,
			Total = delta * Money.ConvertedAmount(amount: amount, rate: rate),
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

	public async Task RecalculateAllForUserAsync(
		Guid userId,
		Core.ValueObjects.Currency baseCurrency,
		CancellationToken ct = default)
	{
		List<(Guid CategoryId, DateOnly Period, decimal Amount, string CurrencyCode, decimal? Rate)> rows =
			await context.GetTransactionRatesForRecalculationAsync(userId: userId, baseCurrencyCode: baseCurrency.Value, ct: ct);

		await context.CategoryTotals.Where(predicate: c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken: ct);

		if (rows.Count == 0)
			return;

		(Guid CategoryId, DateOnly Period, decimal Amount, string CurrencyCode, decimal? Rate) missing = rows.FirstOrDefault(predicate: r => r.Rate is null);
		if (missing.CurrencyCode is not null)
		{
			throw new CurrencyRateMissingException(
				message: $"The exchange rate for {missing.CurrencyCode} > {baseCurrency.Value} was not found.",
				fromCurrency: Core.ValueObjects.Currency.Reconstitute(value: missing.CurrencyCode),
				toCurrency: baseCurrency
			);
		}

		DateTimeOffset now = dateProvider.UtcNow;

		IEnumerable<CategoryTotalEntity> newTotals = rows.GroupBy(keySelector: r => new { r.CategoryId, r.Period })
			.Select(selector: group => new CategoryTotalEntity
			{
				Id = Guid.CreateVersion7(),
				UserId = userId,
				CategoryId = group.Key.CategoryId,
				Period = group.Key.Period,
				Total = group.Sum(selector: r => Money.ConvertedAmount(amount: r.Amount, rate: r.Rate!.Value)),
				TransactionCount = group.Count(),
				RowVersion = 0,
				UpdatedAt = now
			});

		await context.CategoryTotals.AddRangeAsync(entities: newTotals, cancellationToken: ct);
	}
}
