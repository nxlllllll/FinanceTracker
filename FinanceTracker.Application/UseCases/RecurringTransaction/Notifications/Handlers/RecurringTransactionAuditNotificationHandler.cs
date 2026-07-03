using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every recurring transaction lifecycle event.
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
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction created. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, AccountId: {notification.AccountId}, CategoryId: {notification.CategoryId},
			Amount: {notification.Amount}, Direction: {notification.Direction}, DayOfMonth: {notification.DayOfMonth},
			OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionAmountChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction amount changed. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, NewAmount: {notification.NewAmount}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionCurrencyChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction currency changed. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, NewCurrency: {notification.NewCurrency}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionDayOfMonthChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction day of month changed. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, NewDayOfMonth: {notification.NewDayOfMonth}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionActivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction activated. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}

	public Task Handle(RecurringTransactionDeactivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] RecurringTransaction deactivated. RecurringTransactionId: {notification.RecurringTransactionId},
			UserId: {notification.UserId}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}
