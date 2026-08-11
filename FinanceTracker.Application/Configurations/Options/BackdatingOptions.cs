using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// How far back a user may date an operation. Applies to transactions and transfers alike.
/// Bind from <c>appsettings.json</c> under the <c>"Backdating"</c> section.
/// </summary>
public sealed class BackdatingOptions
{
	public const string SectionName = "Backdating";

	/// <summary>How many whole calendar months back an operation may be dated. Default: 3.</summary>
	[Range(minimum: 1, maximum: 12)]
	public int MaxBackdatingMonths { get; init; } = 3;
}
