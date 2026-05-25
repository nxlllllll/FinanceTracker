using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.ProcessedMessage;

public sealed class ProcessedMessageReadRepository(
	FinanceTrackerContext context
) : IProcessedMessageReadRepository
{
	public async Task<bool> IsProcessedAsync(
		Guid messageId,
		string consumerType,
		CancellationToken ct = default)
	{
		return await context.ProcessedMessages.AnyAsync(
			predicate: m => m.MessageId == messageId && m.ConsumerType == consumerType,
			cancellationToken: ct
		);
	}
}
