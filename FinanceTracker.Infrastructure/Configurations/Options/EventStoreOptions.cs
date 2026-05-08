using System.ComponentModel.DataAnnotations;

namespace FinanceTracker.Infrastructure.Configurations.Options;

public sealed class EventStoreOptions
{
	public const string SectionName = "EventStore";

	[Range(minimum: 1, maximum: 1000)]
	public int SnapshotThreshold { get; init; } = 50;
}