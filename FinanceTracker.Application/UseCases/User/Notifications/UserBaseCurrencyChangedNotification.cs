using FinanceTracker.Application.UseCases.User.Commands.ChangeUserBaseCurrency;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

/// <summary>
/// Published by <see cref="ChangeUserBaseCurrencyHandler"/> after a user's base currency is updated.
/// </summary>
/// <param name="OldBaseCurrency">The previous base currency.</param>
/// <param name="NewBaseCurrency">The new base currency.</param>
/// <param name="OccurredAt">UTC timestamp of the change.</param>
public sealed record UserBaseCurrencyChangedNotification(
	Guid UserId,
	Core.ValueObjects.Currency OldBaseCurrency,
	Core.ValueObjects.Currency NewBaseCurrency,
	DateTimeOffset OccurredAt
) : INotification;