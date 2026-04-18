using FinanceTracker.Core.Domains.Abstractions;
using MediatR;

namespace FinanceTracker.Application.Accounts.Notifications;

public sealed record AccountEventsNotification(
	Guid AccountId,
	IReadOnlyList<IEvent> Events
) : INotification;