namespace FinanceTracker.Core.Results;

/// <summary>
/// Represents the absence of a meaningful return value.
/// Used as the success type in <c>Result&lt;Unit, TError&gt;</c> for operations
/// that succeed without producing a value (e.g. state mutations, side effects).
/// </summary>
public readonly struct Unit
{
	/// <summary>The singleton instance. Use instead of <c>new Unit()</c>.</summary>
	public static readonly Unit Default = default;
}
