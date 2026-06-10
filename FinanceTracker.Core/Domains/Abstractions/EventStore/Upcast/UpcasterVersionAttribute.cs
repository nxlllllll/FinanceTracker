namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// Declares the schema version range that an <see cref="EventUpcaster{TFrom,TTo}"/> subclass covers.
/// Required on every class that extends <see cref="EventUpcaster{TFrom,TTo}"/>;
/// construction will throw if the attribute is absent.
/// </summary>
[AttributeUsage(validOn: AttributeTargets.Class, Inherited = false)]
public sealed class UpcasterVersionAttribute(int from, int to) : Attribute
{
	/// <summary>The schema version this upcaster migrates from (inclusive).</summary>
	public int From { get; } = from;

	/// <summary>The schema version this upcaster migrates to (inclusive).</summary>
	public int To { get; } = to;
}