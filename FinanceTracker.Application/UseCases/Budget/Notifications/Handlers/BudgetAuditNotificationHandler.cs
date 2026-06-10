using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Budget.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every user lifecycle event.
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
		logger.ZLogInformation(message: $"[Audit] Budget created. BudgetId: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetAmountChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Budget amount changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetPeriodChangedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Budget period changed: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetActivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Budget activated: {notification}.");
		return Task.CompletedTask;
	}

	public Task Handle(BudgetDeactivatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Budget deactivated: {notification}.");
		return Task.CompletedTask;
	}
}