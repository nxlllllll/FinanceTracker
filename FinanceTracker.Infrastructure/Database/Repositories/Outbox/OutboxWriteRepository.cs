using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class OutboxWriteRepository(
	FinanceTrackerContext context
) : IOutboxWriteRepository
{
	public async Task MarkAsPublishedAsync(
		Guid messageId,
		DateTime processedAt,
		CancellationToken ct = default)
	{
		await context.OutboxMessages.Where(predicate: x => x.Id == messageId).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(
				propertyExpression: x => x.ProcessedAt, 
				valueExpression: processedAt
			),
			cancellationToken: ct
		);
	}

	public async Task MarkAsFailedAsync(
		Guid messageId,
		int retryCount,
		DateTime? failedAt,
		CancellationToken ct = default)
	{
		await context.OutboxMessages.Where(predicate: x => x.Id == messageId).ExecuteUpdateAsync(
			setPropertyCalls: s => s
				.SetProperty(propertyExpression: x => x.RetryCount, valueExpression: retryCount)
				.SetProperty(propertyExpression: x => x.FailedAt, valueExpression: failedAt)
				.SetProperty(propertyExpression: x => x.UpdatedAt, valueExpression: DateTime.UtcNow),
			cancellationToken: ct
		);
	}

	public async Task<int> DeleteProcessedAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.Where(predicate: x => x.ProcessedAt != null && x.ProcessedAt < before)
			.OrderBy(keySelector: x => x.ProcessedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task<int> DeleteFailedAsync(
		DateTime before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.Where(predicate: x => x.FailedAt != null && x.FailedAt < before)
			.OrderBy(keySelector: x => x.FailedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}