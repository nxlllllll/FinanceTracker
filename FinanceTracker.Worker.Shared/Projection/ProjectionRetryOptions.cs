using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.Shared.Projection;

/// <summary>
/// Shared retry configuration for projection workers that retry transient
/// <c>ConcurrencyConflictException</c>s in-process before giving up (as opposed to letting the
/// exception bubble to the broker's native delayed-retry, which is what <c>Worker.TransferProjection</c>
/// does instead). Bind from <c>appsettings.json</c> under the <c>"ProjectionRetry"</c> section.
/// </summary>
public sealed class ProjectionRetryOptions
{
	public const string SectionName = "ProjectionRetry";

	/// <summary>Maximum number of retry attempts on a transient projection failure.</summary>
	[Required]
	public int MaxRetries { get; set; }

	/// <summary>Base delay in milliseconds for exponential backoff between retries.</summary>
	[Required]
	public int BaseDelayMs { get; set; }

	/// <summary>When <c>true</c>, applies jitter to retry delays to prevent thundering herd.</summary>
	[Required]
	public bool UseJitter { get; set; }
}
