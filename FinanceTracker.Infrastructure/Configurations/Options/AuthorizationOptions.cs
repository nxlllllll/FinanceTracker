using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

/// <summary>
/// Root-authority configuration. <see cref="RootUserId"/> is omnipotent — bypasses every
/// permission check unconditionally, including the self-modification guard that applies to
/// everyone else. Deliberately config-only (not manageable via API).
/// Bind from <c>appsettings.json</c>/user-secrets under the <c>"Authorization"</c> section.
/// </summary>
public sealed class AuthorizationOptions
{
	public const string SectionName = "Authorization";

	[Required]
	public Guid RootUserId { get; init; }
}
