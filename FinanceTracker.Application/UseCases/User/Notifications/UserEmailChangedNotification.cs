using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserEmailChangedNotification(
	Guid UserId,
	Email OldEmail,
	Email NewEmail,
	DateTimeOffset OccurredAt
) : INotification;