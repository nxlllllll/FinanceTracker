namespace FinanceTracker.Core.Observability.Correlation;

/// <summary>
/// Holds the correlation ID for the current request scope.
/// The ID is propagated through the MediatR pipeline, RabbitMQ message headers,
/// and structured logs to enable end-to-end request tracing.
/// </summary>
public interface ICorrelationContext
{
	/// <summary>
	/// The correlation ID for the current request.
	/// </summary>
	Guid CorrelationId { get; }

	/// <summary>Sets the correlation ID for this scope.</summary>
	void Set(Guid correlationId);
}
