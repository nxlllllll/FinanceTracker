using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Idempotency;

public sealed class IdempotencyReadRepository(FinanceTrackerContext context) : IIdempotencyReadRepository
{
	public async Task<IdempotencyEntry?> GetAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.IdempotentCommands.AsNoTracking()
			.Where(predicate: e => e.IdempotencyKey == idempotencyKey && e.CommandType == commandType && e.UserId == userId)
			.Select(selector: e => new IdempotencyEntry(ResponseJson: e.ResponseJson, ReservedAt: e.ReservedAt))
			.FirstOrDefaultAsync(cancellationToken: ct);
	}
}