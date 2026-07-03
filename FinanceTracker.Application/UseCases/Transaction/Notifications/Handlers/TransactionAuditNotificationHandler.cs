using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every transaction lifecycle event.
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
		logger.ZLogInformation(message: $"""
			[Audit] Transaction created. TransactionId: {notification.TransactionId},
			UserId: {notification.UserId}, AccountId: {notification.AccountId},
			CategoryId: {notification.CategoryId}, Amount: {notification.Amount},
			Direction: {notification.Direction}, ExchangeRate: {notification.ExchangeRate},
			IsRatePending: {notification.IsRatePending}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionCategoryChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Transaction category changed. TransactionId: {notification.TransactionId},
			UserId: {notification.UserId}, OldCategoryId: {notification.OldCategoryId},
			NewCategoryId: {notification.NewCategoryId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionDescriptionChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Transaction description changed. TransactionId: {notification.TransactionId},
			UserId: {notification.UserId}, OldDescription: {notification.OldDescription},
			NewDescription: {notification.NewDescription}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionExcludedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Transaction excluded. TransactionId: {notification.TransactionId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(TransactionIncludedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Transaction included. TransactionId: {notification.TransactionId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}
