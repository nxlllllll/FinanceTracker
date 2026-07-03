using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Budget.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every budget lifecycle event.
/// </summary>
public sealed class BudgetAuditNotificationHandler(ILogger<BudgetAuditNotificationHandler> logger) :
	INotificationHandler<BudgetCreatedNotification>,
	INotificationHandler<BudgetAmountChangedNotification>,
	INotificationHandler<BudgetPeriodChangedNotification>,
	INotificationHandler<BudgetActivatedNotification>,
	INotificationHandler<BudgetDeactivatedNotification>
{
	public Task Handle(BudgetCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Budget created. BudgetId: {notification.BudgetId}, UserId: {notification.UserId},
			CategoryId: {notification.CategoryId}, Amount: {notification.Amount}, From: {notification.From},
			To: {notification.To}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetAmountChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Budget amount changed. BudgetId: {notification.BudgetId}, UserId: {notification.UserId},
			NewAmount: {notification.NewAmount}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetPeriodChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Budget period changed. BudgetId: {notification.BudgetId}, UserId: {notification.UserId},
			NewFrom: {notification.NewFrom}, NewTo: {notification.NewTo}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetActivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Budget activated. BudgetId: {notification.BudgetId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetDeactivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Budget deactivated. BudgetId: {notification.BudgetId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}
