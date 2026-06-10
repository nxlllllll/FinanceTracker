using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transfer.Notifications.Handlers;

public sealed class TransferAuditNotificationHandler(ILogger<TransferAuditNotificationHandler> logger ) 
	: INotificationHandler<TransferCreatedNotification>
{
	public Task Handle(TransferCreatedNotification notification, CancellationToken cancellationToken)
	{
		logger.ZLogInformation(message: $"[Audit] Transfer created: {notification}.");
		return Task.CompletedTask;
	}
}