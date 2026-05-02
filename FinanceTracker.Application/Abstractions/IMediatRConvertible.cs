using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Application.Abstractions;

public interface IMediatRConvertible : INotificationData
{
	MediatR.INotification ToMediatRNotification();
}