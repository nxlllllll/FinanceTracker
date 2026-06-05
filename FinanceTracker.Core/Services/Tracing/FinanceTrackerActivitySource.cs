using System.Diagnostics;

namespace FinanceTracker.Core.Services.Tracing;

public static class FinanceTrackerActivitySource
{
	public const string Name = "FinanceTracker";
	public static readonly ActivitySource Instance = new ActivitySource(name: Name);

	public static class Operations
	{
		public const string EventStoreSave = "eventstore.save";
		public const string EventStoreLoad = "eventstore.load";
		public const string RabbitMqConsume = "rabbitmq.consume";
	}

	public static class Tags
	{
		public const string AggregateId = "aggregate.id";
		public const string AggregateType = "aggregate.type";
		public const string EventsCount = "events.count";
		public const string SnapshotFound = "snapshot.found";
		public const string EventsLoaded = "events.loaded";
		public const string RequestType = "request.type";
		public const string UserId = "user.id";
	}

	public static class TraceContextHeaders
	{
		public const string TraceParent = "traceparent";
		public const string TraceState = "tracestate";
	}
}