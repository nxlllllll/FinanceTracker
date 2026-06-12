using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transfer.Notifications.Handlers;

/// <summary>
/// Writes a structured audit log entry for every transfer lifecycle event.
/// </summary>
public sealed class TransferAuditNotificationHandler(ILogger<TransferAuditNotificationHandler> logger)
	: INotificationHandler<TransferCreatedNotification>
{
	public Task Handle(TransferCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"""
			[Audit] Transfer created. TransferId: {notification.TransferId}, UserId: {notification.UserId}, FromAccountId: {notification.FromAccountId},
			ToAccountId: {notification.ToAccountId}, AmountFrom: {notification.AmountFrom} {notification.CurrencyFrom},
			AmountTo: {notification.AmountTo} {notification.CurrencyTo}, ExchangeRate: {notification.ExchangeRate},
			IsRatePending: {notification.IsRatePending}, OccurredAt: {notification.OccurredAt:O}.
		""");
		return Task.CompletedTask;
	}
}