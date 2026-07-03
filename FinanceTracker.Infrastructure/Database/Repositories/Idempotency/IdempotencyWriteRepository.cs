using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Idempotency;

public sealed class IdempotencyWriteRepository(FinanceTrackerContext context) : IIdempotencyWriteRepository
{
	public async Task<bool> TryReserveAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		DateTimeOffset reservedAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		return await context.TryReserveIdempotentCommandAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			userId: userId,
			reservedAt: reservedAt,
			expiresAt: expiresAt,
			ct: ct
		);
	}

	public async Task CompleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		string responseJson,
		CancellationToken ct = default)
	{
		await context.IdempotentCommands.Where(predicate: e => e.IdempotencyKey == idempotencyKey && e.CommandType == commandType && e.UserId == userId)
			.ExecuteUpdateAsync(
				setPropertyCalls: s => s.SetProperty(
					propertyExpression: e => e.ResponseJson,
					valueExpression: responseJson
				),
				cancellationToken: ct
			);
	}

	public async Task<int> DeleteExpiredAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.IdempotentCommands.Where(predicate: x => x.ExpiresAt < before)
			.OrderBy(keySelector: x => x.ExpiresAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task DeleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		CancellationToken ct = default)
	{
		await context.IdempotentCommands.Where(
			predicate: e => e.IdempotencyKey == idempotencyKey && e.CommandType == commandType && e.UserId == userId
		).ExecuteDeleteAsync(cancellationToken: ct);
	}
}
