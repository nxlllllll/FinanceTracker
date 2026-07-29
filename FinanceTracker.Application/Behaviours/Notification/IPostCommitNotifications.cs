using MediatR;

namespace FinanceTracker.Application.Behaviours.Notification;

/// <summary>
/// Lets a command handler stage notifications to be published once the request completes
/// successfully.
/// </summary>
public interface IPostCommitNotifications
{
	/// <summary>
	/// Stages <paramref name="notification"/> to be published if the request ultimately succeeds.
	/// Calling this again within the same request queues another one; nothing is replaced.
	/// </summary>
	void Stage(INotification notification);
}
