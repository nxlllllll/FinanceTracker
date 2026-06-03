using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.ProcessedMessage;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;

public sealed class ProcessedMessageWriteRepository(
	FinanceTrackerContext context
) : IProcessedMessageWriteRepository
{
	public async Task MarkAsProcessedAsync(
		Guid messageId,
		string consumerType,
		DateTimeOffset processedAt,
		CancellationToken ct = default)
	{
		await context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = consumerType,
			ProcessedAt = processedAt
		}, cancellationToken: ct);
	}

	public async Task<int> DeleteOldAsync(
		DateTimeOffset before,
		int batchSize,
		CancellationToken ct = default)
	{
		return await context.ProcessedMessages.Where(predicate: x => x.ProcessedAt < before)
			.OrderBy(keySelector: x => x.ProcessedAt)
			.Take(count: batchSize)
			.ExecuteDeleteAsync(cancellationToken: ct);
	}
}