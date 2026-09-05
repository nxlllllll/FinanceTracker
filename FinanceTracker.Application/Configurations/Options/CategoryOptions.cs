using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Configurations.Options;

/// <summary>
/// Bind from <c>appsettings.json</c> under the <c>"Category"</c> section.
/// </summary>
public sealed class CategoryOptions
{
	public const string SectionName = "Category";

	/// <summary>
	/// How many levels a category tree may have, counting the root as one. Default: 4.
	/// </summary>
	[Range(minimum: 1, maximum: 10)]
	public int MaxDepth { get; init; } = 4;
}
