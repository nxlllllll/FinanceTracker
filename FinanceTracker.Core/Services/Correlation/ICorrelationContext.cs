namespace FinanceTracker.Core.Services.Correlation;

public interface ICorrelationContext
{
	Guid CorrelationId { get; }
	void Set(Guid correlationId);
}