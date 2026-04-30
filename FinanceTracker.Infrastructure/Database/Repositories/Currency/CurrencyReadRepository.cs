using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Currency;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyReadRepository(
	FinanceTrackerContext context
) : ICurrencyReadRepository
{
	public async Task<IReadOnlyList<CurrencyDto>> GetAllAsync(CancellationToken ct = default)
	{
		return await context.Currencies.AsNoTracking().Select(selector: currency => new CurrencyDto(
			Code: currency.Code,
			Name: currency.Name,
			Symbol: currency.Symbol,
			IsActive: currency.IsActive
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task<CurrencyDto?> GetByCodeAsync(
		string code,
		CancellationToken ct = default)
	{
		return await context.Currencies.AsNoTracking()
			.Where(predicate: currency => currency.Code == code)
			.Select(selector: currency => new CurrencyDto(
				Code: currency.Code,
				Name: currency.Name,
				Symbol: currency.Symbol,
				IsActive: currency.IsActive
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}