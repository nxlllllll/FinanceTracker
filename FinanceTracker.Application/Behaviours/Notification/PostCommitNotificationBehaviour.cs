using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Notification;

/// <summary>
/// MediatR pipeline behaviour that publishes whatever notification the handler staged via
/// <see cref="IPostCommitNotifications"/> — but only after <paramref name="next"/> returns
/// successfully, and only once.
/// </summary>
public sealed class PostCommitNotificationBehaviour<TRequest, TResponse>(
	IPostCommitNotificationSink notifications,
	IPublisher publisher,
	ILogger<PostCommitNotificationBehaviour<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		TResponse response = await next(t: cancellationToken);

		if (response is IResult { IsFailure: true })
			return response;

		INotification? notification = notifications.TakeStaged();
		if (notification is null)
			return response;

		try
		{
			await publisher.Publish(notification: notification, cancellationToken: CancellationToken.None);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish {notification.GetType().Name}.");
		}

		return response;
	}
}
