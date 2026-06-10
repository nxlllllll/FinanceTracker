using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Core.ValueObjects;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

/// <summary>
/// Published by <see cref="ChangeUserEmailHandler"/> after a user's email is updated.
/// </summary>
/// <param name="OldEmail">The previous email address.</param>
/// <param name="NewEmail">The new email address.</param>
/// <param name="OccurredAt">UTC timestamp of the change.</param>
public sealed record UserEmailChangedNotification(
	Guid UserId,
	Email OldEmail,
	Email NewEmail,
	DateTimeOffset OccurredAt
) : INotification;