namespace FinanceTracker.Core.Services.Correlation;

/// <summary>
/// Marks a MediatR request as carrying a pre-assigned correlation ID.
/// When implemented, <see cref="CorrelationBehaviour{TRequest,TResponse}"/> uses this ID
/// instead of generating a new one, allowing correlation to flow from external systems
/// (e.g. RabbitMQ message headers) into the application pipeline.
/// </summary>
public interface IHasCorrelationId
{
	/// <summary>The correlation ID to propagate through the current request scope.</summary>
	Guid CorrelationId { get; }
}
