using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

/// <summary>
/// Published by <see cref="ChangeUserPasswordHandler"/> after a user's password is updated.
/// Does not carry the new password hash — subscribe only for audit or notification purposes.
/// </summary>
/// <param name="OccurredAt">UTC timestamp of the change.</param>
public sealed record UserPasswordChangedNotification(
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;