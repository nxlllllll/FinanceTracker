using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Core.Domains.Account.Notification;

public sealed record AccountNotification(
	Guid AccountId,
	IReadOnlyList<IEvent> Events
);