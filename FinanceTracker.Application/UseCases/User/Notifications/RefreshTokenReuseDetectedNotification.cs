using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

/// <summary>
/// Published by <see cref="RefreshTokenHandler"/> when an already-revoked refresh token is
/// presented again. A refresh token is single-use by design
/// </summary>
public sealed record RefreshTokenReuseDetectedNotification(
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;
