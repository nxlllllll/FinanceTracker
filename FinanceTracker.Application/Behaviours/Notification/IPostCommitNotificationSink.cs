using MediatR;

namespace FinanceTracker.Application.Behaviours.Notification;

/// <summary>The read side of <see cref="IPostCommitNotifications"/>.</summary>
public interface IPostCommitNotificationSink
{
	/// <summary>Returns the number of notifications staged so far, to be passed to <see cref="TakeFrom"/>.</summary>
	int Mark();

	/// <summary>
	/// Removes and returns everything staged at or after <paramref name="mark"/>, in staging order.
	/// </summary>
	IReadOnlyList<INotification> TakeFrom(int mark);
}
