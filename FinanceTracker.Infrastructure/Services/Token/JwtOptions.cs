using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Services.Token;

/// <summary>
/// Configuration for JWT access token generation.
/// Bind from <c>appsettings.json</c> under the <c>"Jwt"</c> section.
/// </summary>
public sealed class JwtOptions
{
	public const string SectionName = "Jwt";

	/// <summary>HMAC-SHA256 signing secret. Must be at least 32 characters.</summary>
	[Required]
	[MinLength(32)]
	public string Secret { get; init; } = String.Empty;

	/// <summary>JWT <c>iss</c> claim value.</summary>
	[Required]
	public string Issuer { get; init; } = String.Empty;

	/// <summary>JWT <c>aud</c> claim value.</summary>
	[Required]
	public string Audience { get; init; } = String.Empty;

	/// <summary>Access token validity in minutes. Default: 15.</summary>
	[Range(1, 1440)]
	public int AccessTokenTtlMinutes { get; init; } = 15;

	/// <summary>Refresh token validity in days. Default: 7.</summary>
	[Range(1, 365)]
	public int RefreshTokenTtlDays { get; init; } = 7;

	/// <summary>
	/// How long after a rotation a replay of the old refresh token is still treated
	/// as a retry rather than reuse of a stolen token. Default: 30 seconds.
	/// </summary>
	[Range(0, 300)]
	public int RefreshReplayGraceSeconds { get; init; } = 30;
}
