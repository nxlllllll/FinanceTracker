using FinanceTracker.Core.Domains.Abstractions;
using MediatR;

namespace FinanceTracker.Application.Transactions.Notifications;

public sealed record TransactionEventsNotification(
	Guid TransactionId,
	IReadOnlyList<IEvent> Events
) : INotification;