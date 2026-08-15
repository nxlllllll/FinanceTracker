using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Behaviours.Notification;

/// <summary>
/// MediatR pipeline behaviour that publishes whatever notifications the handler staged via
/// <see cref="IPostCommitNotifications"/> — but only after <paramref name="next"/> returns
/// successfully, and only the ones this dispatch staged.
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
		int mark = notifications.Mark();

		TResponse response = await next(t: cancellationToken);

		if (response is IResult { IsFailure: true })
		{
			notifications.TakeFrom(mark: mark);
			return response;
		}
		IReadOnlyList<INotification> staged = notifications.TakeFrom(mark: mark);

		foreach (INotification notification in staged)
		{
			try
			{
				await publisher.Publish(notification: notification, cancellationToken: CancellationToken.None);
			}
			catch (Exception ex)
			{
				string notificationType = notification.GetType().Name;

				FinanceTrackerMetrics.NotificationPublishFailures.Add(
					delta: 1,
					tag: new KeyValuePair<string, object?>(
						key: FinanceTrackerMetrics.Tags.NotificationType,
						value: notificationType
					)
				);

				logger.ZLogError(exception: ex, message: $"Failed to publish {notificationType}.");
			}
		}

		return response;
	}
}
