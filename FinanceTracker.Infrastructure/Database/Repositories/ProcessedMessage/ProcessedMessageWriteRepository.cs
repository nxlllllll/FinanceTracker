using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;

public sealed class ProcessedMessageWriteRepository(
	FinanceTrackerContext context
) : IProcessedMessageWriteRepository
{
	public async Task MarkAsProcessedAsync(
		Guid messageId,
		string consumerType,
		DateTime processedAt,
		CancellationToken ct = default)
	{
		await context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
		{
			MessageId = messageId,
			ConsumerType = consumerType,
			ProcessedAt = processedAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}
}