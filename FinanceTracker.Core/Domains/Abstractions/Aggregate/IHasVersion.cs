namespace FinanceTracker.Core.Domains.Abstractions.Aggregate;

/// <summary>
/// Exposes an entity's current version for optimistic-concurrency
/// checks driven by a client-stated precondition
/// </summary>
public interface IHasVersion
{
	int Version { get; }
}
