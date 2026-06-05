using System.Diagnostics;
using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.RateLimit;
using FinanceTracker.Core.Services.Tracing;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Tracing;

public sealed class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
{
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
