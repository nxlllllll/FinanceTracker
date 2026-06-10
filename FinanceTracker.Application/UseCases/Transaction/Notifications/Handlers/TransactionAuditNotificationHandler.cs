using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every user lifecycle event.
/// </summary>
public sealed class TransactionAuditNotificationHandler(ILogger<TransactionAuditNotificationHandler> logger) :
	INotificationHandler<TransactionCreatedNotification>,
	INotificationHandler<TransactionCategoryChangedNotification>,
	INotificationHandler<TransactionDescriptionChangedNotification>,
	INotificationHandler<TransactionExcludedNotification>,
	INotificationHandler<TransactionIncludedNotification>
{
	public Task Handle(TransactionCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transaction created: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionCategoryChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transaction category changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionDescriptionChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transaction description changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionExcludedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transaction excluded: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionIncludedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transaction included: {notification}.");
		return Task.CompletedTask;
	}
}