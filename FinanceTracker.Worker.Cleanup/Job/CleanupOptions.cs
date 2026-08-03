using System.ComponentModel.DataAnnotations;
using FinanceTracker.Worker.Shared.Job;

namespace FinanceTracker.Worker.Cleanup.Job;

/// <summary>
/// Configuration for <see cref="CleanupJob"/>.
/// Bind from <c>appsettings.json</c> under the <c>"Cleanup"</c> section.
/// </summary>
public sealed class CleanupOptions : IJobOptions
{
	public const string SectionName = "Cleanup";

	/// <inheritdoc/>
	public bool IsEnabled { get; init; } = true;

	public string Group { get; init; } = "cleanup";
	public string TriggerName { get; init; } = "cleanup-trigger";

	/// <summary>How often the cleanup job runs. Default: every 60 minutes.</summary>
	[Range(minimum: 1, maximum: 1440)]
	public int IntervalMinutes { get; init; } = 60;

	/// <summary>Maximum rows deleted per batch to avoid long-running transactions. Default: 1000.</summary>
	[Range(minimum: 1, maximum: 10_000)]
	public int BatchSize { get; init; } = 1000;

	/// <summary>How long processed message records are retained before deletion. Default: 30 days.</summary>
	[Range(minimum: 1, maximum: 365)]
	public int ProcessedMessageRetentionDays { get; init; } = 30;

	/// <summary>How long successfully published outbox records are retained. Default: 7 days.</summary>
	[Range(minimum: 1, maximum: 365)]
	public int OutboxProcessedRetentionDays { get; init; } = 7;

	/// <summary>How long failed outbox records are retained before deletion. Default: 30 days.</summary>
	[Range(minimum: 1, maximum: 365)]
	public int OutboxFailedRetentionDays { get; init; } = 30;

	/// <summary>
	/// How long rows in the account-balance-applied-events idempotency ledger are retained before
	/// deletion. Default: 180 days.
	/// </summary>
	[Range(minimum: 1, maximum: 365)]
	public int AccountBalanceLedgerRetentionDays { get; init; } = 180;

	/// <summary>
	/// How long revoked permissions and removed memberships are kept as tombstones. Default: 180 days.
	/// </summary>
	[Range(minimum: 1, maximum: 365)]
	public int TombstoneRetentionDays { get; init; } = 180;
}
