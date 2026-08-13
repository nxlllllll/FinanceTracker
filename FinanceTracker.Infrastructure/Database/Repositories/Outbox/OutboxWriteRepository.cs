using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Outbox;

public sealed class OutboxWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : IOutboxWriteRepository
{
	public async Task MarkAsPublishedAsync(
		Guid messageId,
		DateTimeOffset processedAt,
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

	/// <inheritdoc/>
	public async Task MarkAsPublishedBatchAsync(
		IReadOnlyCollection<Guid> messageIds,
		DateTimeOffset processedAt,
		CancellationToken ct = default)
	{
		if (messageIds.Count == 0)
			return;

		await context.OutboxMessages.Where(predicate: x => messageIds.Contains(x.Id)).ExecuteUpdateAsync(
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
		DateTimeOffset? failedAt,
		CancellationToken ct = default)
	{
		await context.OutboxMessages.Where(predicate: x => x.Id == messageId).ExecuteUpdateAsync(
			setPropertyCalls: s => s
				.SetProperty(propertyExpression: x => x.RetryCount, valueExpression: retryCount)
				.SetProperty(propertyExpression: x => x.FailedAt, valueExpression: failedAt)
				.SetProperty(propertyExpression: x => x.LockedUntil, valueExpression: (DateTimeOffset?)null)
				.SetProperty(propertyExpression: x => x.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);
	}

	public async Task<int> DeleteProcessedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.Where(predicate: x => x.ProcessedAt != null && x.ProcessedAt < before)
			.OrderBy(keySelector: x => x.ProcessedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task<int> DeleteFailedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.OutboxMessages.Where(predicate: x => x.FailedAt != null && x.FailedAt < before)
			.OrderBy(keySelector: x => x.FailedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}
