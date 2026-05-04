using FinanceTracker.Core.Domains.Abstractions;
using MediatR;

namespace FinanceTracker.Application.Abstractions;

public interface IMediatRConvertible : INotificationData
{
	INotification ToMediatRNotification();
}