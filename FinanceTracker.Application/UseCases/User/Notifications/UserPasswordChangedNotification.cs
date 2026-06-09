using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserPasswordChangedNotification(
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;