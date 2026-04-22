namespace FinanceTracker.Core.Domains.Abstractions;

public interface INotificationDispatcher
{
	Task DispatchAsync(
		AggregateNotification notification, 
		CancellationToken ct = default
	);
}