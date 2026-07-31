using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Api.Configurations;

/// <summary>
/// Where the API hangs its routes. Bind from <c>appsettings.json</c> under <c>"ApiRouting"</c>.
/// </summary>
public sealed class ApiRoutingOptions
{
	public const string SectionName = "ApiRouting";

	/// <summary>Root path for every endpoint. Default: <c>api</c>.</summary>
	[Required]
	public string BasePath { get; init; } = "api";

	/// <summary>API version segment appended to <see cref="BasePath"/>. Default: <c>v1</c>.</summary>
	[Required]
	public string Version { get; init; } = "v1";

	/// <summary>The two combined — <c>/api/v1</c> by default.</summary>
	public string Prefix => $"/{BasePath.Trim('/')}/{Version.Trim('/')}";
}
