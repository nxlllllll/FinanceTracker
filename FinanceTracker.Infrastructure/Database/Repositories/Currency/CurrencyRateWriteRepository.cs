using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Currency;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyRateWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : ICurrencyRateWriteRepository
{
	public async Task UpsertRatesAsync(
		IReadOnlyList<CurrencyRateDto> rates,
		CancellationToken ct = default)
	{
		if (rates.Count == 0)
			return;

		DateTimeOffset now = dateProvider.UtcNow;

		foreach (CurrencyRateDto entry in rates)
		{
			CurrencyRateEntity? existing = await context.CurrencyRates.FirstOrDefaultAsync(
				predicate: r => r.BaseCode == entry.Base && r.TargetCode == entry.Target && r.ActualAt == entry.Date,
				cancellationToken: ct
			);

			if (existing is not null)
				continue;

			await context.CurrencyRates.AddAsync(entity: new CurrencyRateEntity
			{
				BaseCode = entry.Base,
				TargetCode = entry.Target,
				Rate = entry.Rate,
				ActualAt = entry.Date,
				CreatedAt = now
			}, cancellationToken: ct);
		}

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}
