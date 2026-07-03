using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Database.EventStore;

/// <summary>
/// Configuration for <c>PostgresEventStore</c>.
/// Bind from <c>appsettings.json</c> under the <c>"EventStore"</c> section.
/// </summary>
public sealed class EventStoreOptions
{
	public const string SectionName = "EventStore";

	/// <summary>
	/// Number of events after which a snapshot is automatically taken.
	/// A lower value reduces replay time but increases snapshot storage.
	/// Default: 25.
	/// </summary>
	[Range(minimum: 1, maximum: 1000)]
	public int SnapshotThreshold { get; init; } = 25;
}
