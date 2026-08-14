using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryTotalWriteRepository(
	FinanceTrackerContext context,
	IUserQueryRepository userQueryRepository,
	ICurrencyConversionService currencyConversionService,
	IOptionsMonitor<CategoryTotalOptions> options,
	IDateProvider dateProvider
) : ICategoryTotalWriteRepository
{
	/// <summary>Total accumulated for one category within one month while a rebuild is in progress.</summary>
	private sealed record RunningTotal
	{
		public decimal Total { get; set; }
		public int TransactionCount { get; set; }
	}

	/// <summary>
	/// Converts <paramref name="amount"/> into the user's base currency, which is the unit every
	/// category total is denominated in. Kept separate from writing the delta so that a caller
	/// touching two categories with the same amount resolves the rate once instead of per row.
	/// </summary>
	private async Task<decimal> ResolveBaseAmountAsync(
		Guid userId,
		decimal amount,
		Core.ValueObjects.Currency currency,
		DateTimeOffset occurredAt,
		CancellationToken ct)
	{
		UserReadModel user = await userQueryRepository.GetByIdAsync(userId: userId, ct: ct)
			?? throw new NotFoundException(message: "User not found.", id: userId);

		decimal rate = await currencyConversionService.GetStableRateAsync(
			fromCurrency: currency,
			toCurrency: user.BaseCurrency,
			asOf: occurredAt,
			ct: ct
		);

		return Money.ConvertedAmount(amount: amount, rate: rate);
	}

	/// <summary>
	/// Applies a signed delta to one category's monthly total. The upsert increments in place, so
	/// concurrent writers cannot lose each other's contribution.
	/// </summary>
	private Task ApplyDeltaAsync(
		Guid userId,
		Guid categoryId,
		decimal baseAmount,
		int delta,
		DateTimeOffset occurredAt,
		CancellationToken ct)
	{
		DateOnly period = new DateOnly(year: occurredAt.Year, month: occurredAt.Month, day: 1);

		return context.UpsertCategoryTotalAsync(entity: new CategoryTotalEntity
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			CategoryId = categoryId,
			Period = period,
			Total = delta * baseAmount,
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
		decimal baseAmount = await ResolveBaseAmountAsync(
			userId: userId,
			amount: amount,
			currency: currency,
			occurredAt: occurredAt,
			ct: ct
		);

		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			baseAmount: baseAmount,
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
		decimal baseAmount = await ResolveBaseAmountAsync(
			userId: userId,
			amount: amount,
			currency: currency,
			occurredAt: occurredAt,
			ct: ct
		);

		await ApplyDeltaAsync(
			userId: userId,
			categoryId: categoryId,
			baseAmount: baseAmount,
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
		decimal baseAmount = await ResolveBaseAmountAsync(
			userId: userId,
			amount: amount,
			currency: currency,
			occurredAt: occurredAt,
			ct: ct
		);

		await ApplyDeltaAsync(
			userId: userId,
			categoryId: oldCategoryId,
			baseAmount: baseAmount,
			delta: -1,
			occurredAt: occurredAt,
			ct: ct
		);

		await ApplyDeltaAsync(
			userId: userId,
			categoryId: newCategoryId,
			baseAmount: baseAmount,
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
		Dictionary<(Guid CategoryId, DateOnly Period), RunningTotal> totals = await AccumulateTotalsAsync(
			userId: userId,
			baseCurrency: baseCurrency,
			ct: ct
		);

		await ReplaceTotalsAsync(userId: userId, totals: totals, ct: ct);
	}

	private async Task<Dictionary<(Guid CategoryId, DateOnly Period), RunningTotal>> AccumulateTotalsAsync(
		Guid userId,
		Core.ValueObjects.Currency baseCurrency,
		CancellationToken ct)
	{
		int batchSize = options.CurrentValue.RecalculationBatchSize;

		Dictionary<(Guid CategoryId, DateOnly Period), RunningTotal> totals = [];
		Guid cursor = Guid.Empty;
		List<TransactionRateDto> page;

		do
		{
			page = await context.GetTransactionRatesForRecalculationPageAsync(
				userId: userId,
				baseCurrencyCode: baseCurrency.Value,
				afterId: cursor,
				batchSize: batchSize,
				ct: ct
			);

			foreach (TransactionRateDto row in page)
			{
				Accumulate(totals: totals, row: row, baseCurrency: baseCurrency);
				cursor = row.Id;
			}
		}
		while (page.Count == batchSize);

		return totals;
	}

	private static void Accumulate(
		Dictionary<(Guid CategoryId, DateOnly Period), RunningTotal> totals,
		TransactionRateDto row,
		Core.ValueObjects.Currency baseCurrency)
	{
		if (row.Rate is null)
		{
			throw new CurrencyRateMissingException(
				message: $"The exchange rate for {row.CurrencyCode} > {baseCurrency.Value} was not found.",
				fromCurrency: Core.ValueObjects.Currency.Reconstitute(value: row.CurrencyCode),
				toCurrency: baseCurrency
			);
		}

		(Guid CategoryId, DateOnly Period) key = (row.CategoryId, row.Period);

		if (!totals.TryGetValue(key: key, value: out RunningTotal? running))
		{
			running = new RunningTotal();
			totals[key] = running;
		}

		running.Total += Money.ConvertedAmount(amount: row.Amount, rate: row.Rate.Value);
		running.TransactionCount++;
	}

	private async Task ReplaceTotalsAsync(
		Guid userId,
		Dictionary<(Guid CategoryId, DateOnly Period), RunningTotal> totals,
		CancellationToken ct)
	{
		await context.CategoryTotals.Where(predicate: c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken: ct);

		if (totals.Count == 0)
			return;

		DateTimeOffset now = dateProvider.UtcNow;

		IEnumerable<CategoryTotalEntity> newTotals = totals.Select(selector: entry => new CategoryTotalEntity
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			CategoryId = entry.Key.CategoryId,
			Period = entry.Key.Period,
			Total = entry.Value.Total,
			TransactionCount = entry.Value.TransactionCount,
			RowVersion = 0,
			UpdatedAt = now
		});

		await context.CategoryTotals.AddRangeAsync(entities: newTotals, cancellationToken: ct);
	}
}
