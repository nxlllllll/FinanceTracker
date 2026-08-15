namespace FinanceTracker.Api.Configurations;

/// <summary>
/// The per-IP ceiling applied to every request, before authentication runs.
/// </summary>
public sealed class IpRateLimitOptions
{
	public const string SectionName = "IpRateLimit";

	/// <summary>Turns enforcement off while leaving the middleware in place.</summary>
	public bool Enabled { get; init; } = true;

	/// <summary>Requests admitted from one address per window.</summary>
	public int RequestsPerWindow { get; init; } = 300;

	/// <summary>Length of the sliding window, in seconds.</summary>
	public int WindowSeconds { get; init; } = 60;
}
