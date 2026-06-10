using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every user lifecycle event.
/// </summary>
public sealed class RecurringTransactionAuditNotificationHandler(ILogger<RecurringTransactionAuditNotificationHandler> logger) :
	INotificationHandler<RecurringTransactionCreatedNotification>,
	INotificationHandler<RecurringTransactionAmountChangedNotification>,
	INotificationHandler<RecurringTransactionCurrencyChangedNotification>,
	INotificationHandler<RecurringTransactionDayOfMonthChangedNotification>,
	INotificationHandler<RecurringTransactionActivatedNotification>,
	INotificationHandler<RecurringTransactionDeactivatedNotification>
{
	public Task Handle(RecurringTransactionCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction created: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionAmountChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction amount changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionCurrencyChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction currency changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionDayOfMonthChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction day of month changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionActivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction activated: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionDeactivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] RecurringTransaction deactivated: {notification}.");
		return Task.CompletedTask;
	}
}