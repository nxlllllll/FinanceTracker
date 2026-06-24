using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Configuration for <c>AuthRateLimitingBehaviour</c>.
/// Defines the sliding-window rate limits applied to pre-authentication commands
/// (login, registration, token refresh/revocation) that implement
/// <see cref="IIpScopedRequest"/> and/or <see cref="IEmailScopedRequest"/>.
/// Both limits are checked independently — either one being exceeded blocks the request.
/// Bind from <c>appsettings.json</c> under the <c>"AnonymousRateLimit"</c> section.
/// </summary>
public sealed class AnonymousRateLimitOptions
{
	public const string SectionName = "AnonymousRateLimit";

	/// <summary>Maximum number of requests allowed per IP address within the window. Default: 20.</summary>
	[Range(minimum: 1, maximum: 10000)]
	public int IpRequestsPerWindow { get; init; } = 20;

	/// <summary>Duration of the per-IP sliding window in seconds. Default: 300 (5 minutes).</summary>
	[Range(minimum: 1, maximum: 86400)]
	public int IpWindowSeconds { get; init; } = 300;

	/// <summary>Maximum number of requests allowed per email address within the window. Default: 5.</summary>
	[Range(minimum: 1, maximum: 10000)]
	public int EmailRequestsPerWindow { get; init; } = 5;

	/// <summary>Duration of the per-email sliding window in seconds. Default: 300 (5 minutes).</summary>
	[Range(minimum: 1, maximum: 86400)]
	public int EmailWindowSeconds { get; init; } = 300;
}