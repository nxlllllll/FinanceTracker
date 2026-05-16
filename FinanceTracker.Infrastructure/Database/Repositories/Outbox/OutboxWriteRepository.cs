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
		await context.OutboxMessages
			.Where(predicate: m => m.Id == messageId)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(e => e.ProcessedAt, processedAt),
				cancellationToken: ct
			);
	}

	public async Task MarkAsFailedAsync(
		Guid messageId,
		int retryCount,
		DateTime? failedAt,
		CancellationToken ct = default)
	{
		await context.OutboxMessages
			.Where(predicate: m => m.Id == messageId)
			.ExecuteUpdateAsync(
				setPropertyCalls: builder => builder
					.SetProperty(e => e.RetryCount, retryCount)
					.SetProperty(e => e.FailedAt, failedAt),
				cancellationToken: ct
			);
	}
}