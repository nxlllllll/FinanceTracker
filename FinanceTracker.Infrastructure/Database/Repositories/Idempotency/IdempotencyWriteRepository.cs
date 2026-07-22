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
		Guid reservationId,
		DateTimeOffset reservedAt,
		DateTimeOffset expiresAt,
		CancellationToken ct = default)
	{
		return await context.TryReserveIdempotentCommandAsync(
			idempotencyKey: idempotencyKey,
			commandType: commandType,
			userId: userId,
			reservationId: reservationId,
			reservedAt: reservedAt,
			expiresAt: expiresAt,
			ct: ct
		);
	}

	public async Task<bool> CompleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		Guid reservationId,
		string responseJson,
		CancellationToken ct = default)
	{
		int affected = await context.IdempotentCommands.Where(
			predicate: e => e.IdempotencyKey == idempotencyKey && e.CommandType == commandType && e.UserId == userId && e.ReservationId == reservationId
		).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(
				propertyExpression: e => e.ResponseJson,
				valueExpression: responseJson
			),
			cancellationToken: ct
		);

		return affected > 0;
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

	public async Task<bool> DeleteAsync(
		Guid idempotencyKey,
		string commandType,
		Guid userId,
		Guid reservationId,
		CancellationToken ct = default)
	{
		int affected = await context.IdempotentCommands.Where(
			predicate: e => e.IdempotencyKey == idempotencyKey && e.CommandType == commandType && e.UserId == userId && e.ReservationId == reservationId
		).ExecuteDeleteAsync(cancellationToken: ct);

		return affected > 0;
	}
}
