using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserTimeZoneChangedNotification(
	Guid UserId,
	TimeZoneId OldTimeZone,
	TimeZoneId NewTimeZone,
	DateTimeOffset OccurredAt
) : INotification;
