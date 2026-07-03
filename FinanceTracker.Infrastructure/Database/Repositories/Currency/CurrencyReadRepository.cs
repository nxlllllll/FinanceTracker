using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyReadRepository(
	FinanceTrackerContext context
) : ICurrencyReadRepository
{
	public async Task<IReadOnlyList<CurrencyInfo>> GetAllAsync(CancellationToken ct = default)
	{
		return await context.Currencies.AsNoTracking().Select(selector: currency => new CurrencyInfo(
			Code: currency.Code,
			Name: currency.Name,
			Symbol: currency.Symbol,
			IsActive: currency.IsActive
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<CurrencyInfo>> GetAllActiveAsync(CancellationToken ct = default)
	{
		return await context.Currencies.AsNoTracking().Where(predicate: currency => currency.IsActive).Select(selector: currency => new CurrencyInfo(
			Code: currency.Code,
			Name: currency.Name,
			Symbol: currency.Symbol,
			IsActive: currency.IsActive
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<CurrencyInfo?> GetByCodeAsync(
		string code,
		CancellationToken ct = default)
	{
		return await context.Currencies.AsNoTracking().Where(predicate: currency => currency.Code == code)
			.Select(selector: currency => new CurrencyInfo(
				Code: currency.Code,
				Name: currency.Name,
				Symbol: currency.Symbol,
				IsActive: currency.IsActive
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<bool> ExistsAsync(string code, CancellationToken ct = default)
		=> await context.Currencies.AnyAsync(predicate: c => c.Code == code && c.IsActive, cancellationToken: ct);
}
