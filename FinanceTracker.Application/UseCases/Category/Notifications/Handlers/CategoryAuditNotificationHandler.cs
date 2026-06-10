using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Category.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every user lifecycle event.
/// </summary>
public sealed class CategoryAuditNotificationHandler(ILogger<CategoryAuditNotificationHandler> logger) :
	INotificationHandler<CategoryCreatedNotification>,
	INotificationHandler<CategoryRenamedNotification>,
	INotificationHandler<CategoryArchivedNotification>,
	INotificationHandler<CategoryUnarchivedNotification>
{
	public Task Handle(CategoryCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Category created: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryRenamedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Category renamed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryArchivedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Category archived: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(CategoryUnarchivedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Category unarchived: {notification}.");
		return Task.CompletedTask;
	}
}