using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.Repositories.CurrencyRate;

public sealed class CurrencyRateWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider,
	IUnitOfWork unitOfWork,
	ILogger<CurrencyRateWriteRepository> logger
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

		await unitOfWork.ExecuteInTransactionAsync(
			operation: async () => await context.SaveChangesAsync(cancellationToken: ct), 
			onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to create currency rates."),
			ct: ct
		);
	}
}
