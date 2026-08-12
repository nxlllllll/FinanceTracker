using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed class BaseCurrencyRecalculationReadRepository(
	FinanceTrackerContext context
) : IBaseCurrencyRecalculationReadRepository
{
	public async Task<bool> TotalsAreUnavailableAsync(
		Guid userId,
		CancellationToken ct = default)
	{
		BaseCurrencyRecalculationStatus? status = await context.BaseCurrencyRecalculations.AsNoTracking()
			.Where(predicate: r => r.UserId == userId)
			.Select(selector: r => (BaseCurrencyRecalculationStatus?)r.Status)
			.FirstOrDefaultAsync(cancellationToken: ct);

		return status is not null && status.Value.TotalsAreUnavailable();
	}
}
