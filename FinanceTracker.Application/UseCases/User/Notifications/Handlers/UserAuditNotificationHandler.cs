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
		logger.ZLogInformation(message: $"[Audit] User registered: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(UserEmailChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] User email changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(UserPasswordChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] User password changed: {notification}.");
		return Task.CompletedTask;
		
	}

	public Task Handle(UserBaseCurrencyChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] User base currency changed: {notification}.");
		return Task.CompletedTask;
	}
}