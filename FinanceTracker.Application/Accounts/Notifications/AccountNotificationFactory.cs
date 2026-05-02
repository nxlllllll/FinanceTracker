using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;

namespace FinanceTracker.Application.Accounts.Notifications;

public sealed class AccountNotificationFactory : IAggregateNotificationFactory
{
	public string AggregateType => AggregateTypeNames.Account;

	public IAppNotification Build(Guid aggregateId, IReadOnlyList<IEvent> events)
		=> new AccountAppNotification(AccountId: aggregateId, Events: events);
}