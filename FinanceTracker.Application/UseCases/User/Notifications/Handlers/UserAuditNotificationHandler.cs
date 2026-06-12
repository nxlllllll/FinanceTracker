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
	INotificationHandler<UserBaseCurrencyChangedNotification>
{
	public Task Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User registered. UserId: {notification.UserId}, Email: {notification.Email},
			BaseCurrency: {notification.BaseCurrency}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(UserEmailChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] User email changed. UserId: {notification.UserId}, OldEmail: {notification.OldEmail},
			NewEmail: {notification.NewEmail}, OccurredAt: {notification.OccurredAt:O}.
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
}