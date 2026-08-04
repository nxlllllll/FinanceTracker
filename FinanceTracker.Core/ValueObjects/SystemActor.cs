namespace FinanceTracker.Core.ValueObjects;

/// <summary>
/// Identifies the system itself as the actor behind an operation nobody initiated.
/// </summary>
public static class SystemActor
{
	public static readonly Guid Id = Guid.Empty;
}
