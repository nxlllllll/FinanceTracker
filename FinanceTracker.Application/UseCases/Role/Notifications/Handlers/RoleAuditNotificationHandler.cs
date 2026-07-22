using FinanceTracker.Application.UseCases.Role.Notifications;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Role.Notifications.Handlers;

public sealed class RoleAuditNotificationHandler(
	ILogger<RoleAuditNotificationHandler> logger
) : INotificationHandler<RoleCreatedNotification>,
	INotificationHandler<RoleAssignedToUserNotification>,
	INotificationHandler<RoleRemovedFromUserNotification>
{
	public Task Handle(
		RoleCreatedNotification notification,
		CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Role created. RoleId: {notification.RoleId}, DisplayName: {notification.DisplayName},
			Permissions: [{String.Join(separator: ", ", values: notification.Permissions)}], OccurredAt: {notification.OccurredAt}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(
		RoleAssignedToUserNotification notification,
		CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Role assigned. UserId: {notification.UserId}, RoleId: {notification.RoleId},
			AssignedBy: {notification.AssignedBy}, OccurredAt: {notification.OccurredAt}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(
		RoleRemovedFromUserNotification notification,
		CancellationToken ct = default)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Role removed. UserId: {notification.UserId}, RoleId: {notification.RoleId},
			RemovedBy: {notification.RemovedBy}, OccurredAt: {notification.OccurredAt}.
		""");
		return Task.CompletedTask;
	}
}
