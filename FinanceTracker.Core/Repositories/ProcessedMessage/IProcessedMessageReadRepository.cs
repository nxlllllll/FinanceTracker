namespace FinanceTracker.Core.Repositories.ProcessedMessage;

public interface IProcessedMessageReadRepository
{
	Task<bool> IsProcessedAsync(
		Guid messageId,
		string consumerType,
		CancellationToken ct = default
	);
}
