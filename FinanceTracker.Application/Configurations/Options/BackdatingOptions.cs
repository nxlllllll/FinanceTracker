using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// Which dates a user may give a transaction. Transfers are dated by the server and take none.
/// Bind from <c>appsettings.json</c> under the <c>"Backdating"</c> section.
/// </summary>
public sealed class BackdatingOptions
{
	public const string SectionName = "Backdating";

	/// <summary>How many whole calendar months back an operation may be dated. Default: 3.</summary>
	[Range(minimum: 1, maximum: 12)]
	public int MaxBackdatingMonths { get; init; } = 3;

	/// <summary>How many seconds ahead of the server clock a date may be before it counts as the future. Default: 3.</summary>
	[Range(minimum: 0, maximum: 3)]
	public int FutureToleranceSeconds { get; init; } = 3;
}
