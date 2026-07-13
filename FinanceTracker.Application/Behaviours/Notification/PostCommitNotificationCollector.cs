using MediatR;

namespace FinanceTracker.Application.Behaviours.Notification;

internal sealed class PostCommitNotificationCollector : IPostCommitNotifications, IPostCommitNotificationSink
{
	private INotification? _staged;

	public void Stage(INotification notification)
		=> _staged = notification;

	public INotification? TakeStaged()
	{
		INotification? notification = _staged;
		_staged = null;
		return notification;
	}
}
