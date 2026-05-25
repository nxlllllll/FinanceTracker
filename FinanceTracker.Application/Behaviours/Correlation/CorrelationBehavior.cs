using FinanceTracker.Core.Services.Correlation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Behaviours.Correlation;

public sealed class CorrelationBehavior<TRequest, TResponse>(
	ICorrelationContext correlationContext,
	ILogger<CorrelationBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : notnull
{
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is IHasCorrelationId { CorrelationId: var id } && id != Guid.Empty)
			correlationContext.Set(correlationId: id);
		else
			correlationContext.Set(correlationId: Guid.CreateVersion7());

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object>
		{
			["CorrelationId"] = correlationContext.CorrelationId,
			["RequestType"] = typeof(TRequest).Name
		});
		
		return await next(t: cancellationToken);
	}
}
