using FinanceTracker.Core.Repositories.DomainEventOutbox;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.DomainEventOutbox;

public sealed class DomainEventOutboxWriteRepository(
	FinanceTrackerContext context
) : IDomainEventOutboxWriteRepository
{
	public async Task MarkAsProcessedAsync(
		Guid id,
		DateTimeOffset processedAt,
		CancellationToken ct = default)
	{
		await context.DomainEventOutbox.Where(predicate: e => e.Id == id).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(propertyExpression: e => e.ProcessedAt, valueExpression: processedAt),
			cancellationToken: ct
		);
	}

	public async Task MarkAsFailedAsync(
		Guid id,
		int retryCount,
		DateTimeOffset? failedAt,
		CancellationToken ct = default)
	{
		await context.DomainEventOutbox.Where(predicate: e => e.Id == id).ExecuteUpdateAsync(
			setPropertyCalls: s => s
				.SetProperty(propertyExpression: e => e.RetryCount, valueExpression: retryCount)
				.SetProperty(propertyExpression: e => e.FailedAt, valueExpression: failedAt),
			cancellationToken: ct
		);
	}

	public async Task<int> DeleteProcessedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.DomainEventOutbox.Where(predicate: e => e.ProcessedAt != null && e.ProcessedAt < before)
			.OrderBy(keySelector: e => e.ProcessedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}

	public async Task<int> DeleteFailedAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.DomainEventOutbox.Where(predicate: e => e.FailedAt != null && e.FailedAt < before)
			.OrderBy(keySelector: e => e.FailedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}
