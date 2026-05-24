using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Idempotency;

public sealed class IdempotencyWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IIdempotencyWriteRepository
{
	public async Task StoreAsync(
		Guid idempotencyKey,
		string commandType,
		string responseJson,
		DateTime expiresAt,
		CancellationToken ct = default)
	{
		await context.InsertIdempotentCommandAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			responseJson: responseJson,
			createdAt: dateProvider.UtcNow,
			expiresAt: expiresAt,
			ct: ct
		);
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