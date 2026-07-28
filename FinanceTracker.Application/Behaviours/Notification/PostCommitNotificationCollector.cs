using MediatR;

namespace FinanceTracker.Application.Behaviours.Notification;

internal sealed class PostCommitNotificationCollector : IPostCommitNotifications, IPostCommitNotificationSink
{
	private readonly List<INotification> _staged = [];

	public void Stage(INotification notification)
		=> _staged.Add(item: notification);

	public int Mark() => _staged.Count;

	public IReadOnlyList<INotification> TakeFrom(int mark)
	{
		if (mark >= _staged.Count)
			return [];

		List<INotification> taken = _staged.GetRange(index: mark, count: _staged.Count - mark);
		_staged.RemoveRange(index: mark, count: _staged.Count - mark);

		return taken;
	}
}
