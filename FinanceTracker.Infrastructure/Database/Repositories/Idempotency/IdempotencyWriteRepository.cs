using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

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
		await context.IdempotentCommands.AddAsync(entity: new IdempotentCommandEntity
		{
			IdempotencyKey = idempotencyKey,
			CommandType = commandType,
			ResponseJson = responseJson,
			CreatedAt = dateProvider.UtcNow,
			ExpiresAt = expiresAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}