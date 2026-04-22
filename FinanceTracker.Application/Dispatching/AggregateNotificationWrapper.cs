using FinanceTracker.Core.Domains.Abstractions;
using MediatR;

namespace FinanceTracker.Application.Dispatching;

public sealed record AggregateNotificationWrapper(
	AggregateNotification Notification
) : INotification;