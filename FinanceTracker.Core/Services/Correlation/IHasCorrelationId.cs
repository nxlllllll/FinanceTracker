namespace FinanceTracker.Core.Services.Correlation;

public interface IHasCorrelationId
{
	Guid CorrelationId { get; }
}
