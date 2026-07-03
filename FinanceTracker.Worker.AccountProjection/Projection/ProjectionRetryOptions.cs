using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Worker.AccountProjection.Projection;

/// <summary>
/// Configuration for projection retry behaviour in the account projection worker.
/// Controls how many times a failed projection attempt is retried before the message
/// is nacked and sent to the dead-letter exchange.
/// Bind from <c>appsettings.json</c> under the <c>"ProjectionRetry"</c> section.
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
