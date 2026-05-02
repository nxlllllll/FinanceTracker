using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Application.Accounts.Notifications;

public sealed record AccountAppNotification(
	Guid AccountId,
	IReadOnlyList<IEvent> Events
) : IAppNotification
{
	public INotificationData Data { get; } = new AccountEvents(AccountId: AccountId, Events: Events);
}