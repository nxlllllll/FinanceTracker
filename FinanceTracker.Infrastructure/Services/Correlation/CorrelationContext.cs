using FinanceTracker.Core.Services.Correlation;

namespace FinanceTracker.Infrastructure.Services.Correlation;

/// <summary>
/// Scoped implementation of <see cref="ICorrelationContext"/>.
/// Initialised with a new <c>Guid.CreateVersion7()</c> per request scope
/// and overwritten by <c>CorrelationBehavior</c> or RabbitMQ consumer header extraction.
/// </summary>
public sealed class CorrelationContext : ICorrelationContext
{
	public Guid CorrelationId { get; private set; } = Guid.CreateVersion7();
 
	public void Set(Guid correlationId) 
		=> CorrelationId = correlationId;
}
