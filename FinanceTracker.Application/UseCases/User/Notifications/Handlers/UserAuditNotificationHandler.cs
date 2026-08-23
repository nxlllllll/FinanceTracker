using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.User.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every user lifecycle event.
/// </summary>
public sealed class UserAuditNotificationHandler(ILogger<UserAuditNotificationHandler> logger) :
	INotificationHandler<UserRegisteredNotification>,
	INotificationHandler<UserEmailChangedNotification>,
	INotificationHandler<UserPasswordChangedNotification>,
	INotificationHandler<UserBaseCurrencyChangedNotification>,
	INotificationHandler<UserTimeZoneChangedNotification>,
	INotificationHandler<RefreshTokenReuseDetectedNotification>
{
	public Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User registered. UserId: {notification.UserId}, Email: {notification.Email.Masked},
			BaseCurrency: {notification.BaseCurrency}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(UserEmailChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User email changed. UserId: {notification.UserId}, OldEmail: {notification.OldEmail.Masked},
			NewEmail: {notification.NewEmail.Masked}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(UserPasswordChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User password changed. UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(UserBaseCurrencyChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User base currency changed. UserId: {notification.UserId}, OldBaseCurrency: {notification.OldBaseCurrency},
			NewBaseCurrency: {notification.NewBaseCurrency}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RefreshTokenReuseDetectedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogWarning(message: $"""
			[Security] Refresh token reuse detected — all active sessions revoked.
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(UserTimeZoneChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User time zone changed. UserId: {notification.UserId}, OldTimeZone: {notification.OldTimeZone},
			NewTimeZone: {notification.NewTimeZone}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}
