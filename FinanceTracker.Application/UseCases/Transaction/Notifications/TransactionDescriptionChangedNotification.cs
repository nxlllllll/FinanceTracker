using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionDescriptionChangedNotification(
	Guid TransactionId,
	Guid UserId,
	string? OldDescription,
	string? NewDescription,
	DateTimeOffset OccurredAt
) : INotification;
