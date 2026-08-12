using FinanceTracker.Core.Observability.Correlation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Application.Behaviours.Correlation;

/// <summary>
/// MediatR pipeline behaviour that sets up the correlation ID for each request.
/// If the request implements <see cref="IHasCorrelationId"/> with a non-empty ID, that ID takes
/// precedence — enabling end-to-end tracing from external triggers (e.g. RabbitMQ). Otherwise, if
/// a correlation ID hasn't already been established for this scope (e.g. by an HTTP-layer
/// <c>CorrelationIdMiddleware</c> running ahead of this behaviour), a new one is generated —
/// this behaviour never blindly overwrites an ID some earlier stage already set.
/// The ID is also added to the structured log scope for every log statement in the pipeline.
/// </summary>
public sealed class CorrelationBehaviour<TRequest, TResponse>(
	ICorrelationContext correlationContext,
	ILogger<CorrelationBehaviour<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
	where TRequest : notnull
	where TResponse : notnull
{
	/// <inheritdoc/>
	public async Task<TResponse> Handle(
		TRequest request,
		RequestHandlerDelegate<TResponse> next,
		CancellationToken cancellationToken = default)
	{
		if (request is IHasCorrelationId { CorrelationId: var id } && id != Guid.Empty)
			correlationContext.Set(correlationId: id);
		else if (correlationContext.CorrelationId == Guid.Empty)
			correlationContext.Set(correlationId: Guid.CreateVersion7());

		using IDisposable? scope = logger.BeginScope(state: new Dictionary<string, object>
		{
			["CorrelationId"] = correlationContext.CorrelationId,
			["RequestType"] = typeof(TRequest).Name
		});

		return await next(t: cancellationToken);
	}
}
