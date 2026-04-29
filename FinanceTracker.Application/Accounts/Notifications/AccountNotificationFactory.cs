using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Notification;

namespace FinanceTracker.Application.Accounts.Notifications;

public sealed class AccountNotificationFactory : IAggregateNotificationFactory
{
	public string AggregateType => nameof(Account);

	public object Build(Guid aggregateId, IReadOnlyList<IEvent> events)
		=> new AccountNotification(AccountId: aggregateId, Events: events);
}