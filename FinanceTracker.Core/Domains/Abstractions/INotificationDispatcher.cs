namespace FinanceTracker.Core.Domains.Abstractions;

public interface INotificationDispatcher
{
	Task DispatchAsync(
		IAppNotification appNotification, 
		CancellationToken ct = default
	);
}