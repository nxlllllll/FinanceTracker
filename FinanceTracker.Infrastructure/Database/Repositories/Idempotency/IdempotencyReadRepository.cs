using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Idempotency;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Idempotency;

public sealed class IdempotencyReadRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IIdempotencyReadRepository
{
	public async Task<string?> GetAsync(
		Guid idempotencyKey,
		CancellationToken ct = default)
	{
		IdempotentCommandEntity? entity = await context.IdempotentCommands
			.Where(predicate: e => e.IdempotencyKey == idempotencyKey && e.ExpiresAt > dateProvider.UtcNow)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (entity is null)
			return null;

		return entity.ResponseJson ?? String.Empty;
	}
}