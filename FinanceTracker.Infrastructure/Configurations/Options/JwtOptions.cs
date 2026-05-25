using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class JwtOptions
{
	public const string SectionName = "Jwt";

	[Required]
	[MinLength(32)]
	public string Secret { get; init; } = string.Empty;

	[Required]
	public string Issuer { get; init; } = string.Empty;

	[Required]
	public string Audience { get; init; } = string.Empty;

	[Range(1, 1440)]
	public int AccessTokenTtlMinutes { get; init; } = 15;

	[Range(1, 365)]
	public int RefreshTokenTtlDays { get; init; } = 7;
}
