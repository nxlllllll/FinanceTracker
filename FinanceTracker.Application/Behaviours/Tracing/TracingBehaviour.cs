using System.Diagnostics;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Services.Tracing;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Tracing;

/// <summary>
/// MediatR pipeline behaviour that creates an OpenTelemetry span for each request.
/// Tags the span with the request type name and user ID (when the request implements
/// <see cref="IAuthorizable"/>). Sets the span status to <c>Error</c> on exception.
/// </summary>
public sealed class TracingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
	/// <inheritdoc/>
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		using Activity? activity = FinanceTrackerActivitySource.Instance.StartActivity(name: typeof(TRequest).Name, kind: ActivityKind.Internal);

		activity?.SetTag(key: FinanceTrackerActivitySource.Tags.RequestType, value: typeof(TRequest).Name);
		if (request is IAuthorizable authorizable)
			activity?.SetTag(key: FinanceTrackerActivitySource.Tags.UserId, value: authorizable.UserId);

		try
		{
			TResponse response = await next(t: cancellationToken);
			activity?.SetStatus(code: ActivityStatusCode.Ok);
			return response;
		}
		catch (Exception ex)
		{
			activity?.SetStatus(code: ActivityStatusCode.Error, description: ex.Message);
			activity?.AddException(exception: ex);
			throw;
		}
	}
}
