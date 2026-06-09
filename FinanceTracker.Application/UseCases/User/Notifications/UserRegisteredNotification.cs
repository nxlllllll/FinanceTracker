using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserRegisteredNotification(
	Guid UserId,
	Email Email,
	Core.ValueObjects.Currency BaseCurrency,
	DateTimeOffset OccurredAt
) : INotification;