using FinanceTracker.Application.Abstractions;
using FinanceTracker.Core.Domains.Abstractions;
using INotification = MediatR.INotification;

namespace FinanceTracker.Application.Accounts.Notifications;

public sealed record AccountEvents(
	Guid AccountId,
	IReadOnlyList<IEvent> Events
) : IMediatRConvertible
{
	public INotification ToMediatRNotification()
		=> new AccountEventsNotification(AccountId: AccountId, Events: Events);
}