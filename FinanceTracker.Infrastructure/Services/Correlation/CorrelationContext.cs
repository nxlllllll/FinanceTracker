using FinanceTracker.Core.Services.Correlation;

namespace FinanceTracker.Infrastructure.Services.Correlation;

public sealed class CorrelationContext : ICorrelationContext
{
	public Guid CorrelationId { get; private set; } = Guid.CreateVersion7();
 
	public void Set(Guid correlationId) 
		=> CorrelationId = correlationId;
}
