using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Idempotency;

public sealed class IdempotencyWriteRepository(
	FinanceTrackerContext context
) : IIdempotencyWriteRepository
{
	public async Task StoreAsync(
		Guid idempotencyKey,
		string commandType,
		string responseJson,
		DateTime expiresAt,
		CancellationToken ct = default)
	{
		await context.IdempotentCommands.AddAsync(entity: new IdempotentCommandEntity
		{
			IdempotencyKey = idempotencyKey,
			CommandType = commandType,
			ResponseJson = responseJson,
			CreatedAt = DateTime.UtcNow,
			ExpiresAt = expiresAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task<int> DeleteExpiredAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.IdempotentCommands.Where(predicate: x => x.ExpiresAt < before)
			.OrderBy(keySelector: x => x.ExpiresAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}