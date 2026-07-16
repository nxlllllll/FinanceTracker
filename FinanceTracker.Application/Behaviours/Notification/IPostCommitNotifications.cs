using MediatR;

namespace FinanceTracker.Application.Behaviours.Notification;

/// <summary>
/// Lets a command handler stage a notification to be
/// published once the request completes successfully
/// </summary>
public interface IPostCommitNotifications
{
	/// <summary>
	/// Stages <paramref name="notification"/> to be published if the request ultimately
	/// succeeds. Calling this again within the same request <em>replaces</em> the
	/// previously staged notification rather than queuing a second one
	/// </summary>
	void Stage(INotification notification);
}

/// <summary>The read side of <see cref="IPostCommitNotifications"/></summary>
public interface IPostCommitNotificationSink
{
	/// <summary>Returns the staged notification, if any, and clears the slot.</summary>
	INotification? TakeStaged();
}
