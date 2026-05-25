using System.Diagnostics;
using FinanceTracker.Core.Tracing;
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

		activity?.SetTag(key: "request.type", value: typeof(TRequest).Name);

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
