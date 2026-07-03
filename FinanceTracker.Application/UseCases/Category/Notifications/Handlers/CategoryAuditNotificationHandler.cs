using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Category.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every category lifecycle event.
/// </summary>
public sealed class CategoryAuditNotificationHandler(ILogger<CategoryAuditNotificationHandler> logger) :
	INotificationHandler<CategoryCreatedNotification>,
	INotificationHandler<CategoryRenamedNotification>,
	INotificationHandler<CategoryArchivedNotification>,
	INotificationHandler<CategoryUnarchivedNotification>
{
	public Task Handle(CategoryCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Category created. CategoryId: {notification.CategoryId}, UserId: {notification.UserId},
			Name: {notification.Name}, Type: {notification.Type}, ParentId: {notification.ParentId},
			OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryRenamedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Category renamed. CategoryId: {notification.CategoryId}, 
			UserId: {notification.UserId}, OldName: {notification.OldName}, 
			NewName: {notification.NewName}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryArchivedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Category archived. CategoryId: {notification.CategoryId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryUnarchivedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Category unarchived. CategoryId: {notification.CategoryId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}
