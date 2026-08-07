namespace FinanceTracker.Core.Persistence;

/// <summary>
/// Decides whether a failure is worth trying again.
/// </summary>
public interface ITransientFaultDetector
{
	bool IsTransient(Exception exception);
}
