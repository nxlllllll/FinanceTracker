namespace FinanceTracker.Core.Domains.Abstractions;

public interface INotificationDispatcher
{
	Task DispatchAsync(
		Notification notification, 
		CancellationToken ct = default
	);
}