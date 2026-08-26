using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// How long after a transaction was recorded it may still be cancelled.
/// Bind from <c>appsettings.json</c> under the <c>"Cancellation"</c> section.
/// </summary>
public sealed class CancellationOptions
{
	public const string SectionName = "Cancellation";

	/// <summary>How many days after being recorded an operation may be cancelled. Default: 30.</summary>
	[Range(minimum: 1, maximum: 365)]
	public int MaxAgeDays { get; init; } = 30;
}
