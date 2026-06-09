using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserBaseCurrencyChangedNotification(
	Guid UserId,
	Core.ValueObjects.Currency OldBaseCurrency,
	Core.ValueObjects.Currency NewBaseCurrency,
	DateTimeOffset OccurredAt
) : INotification;