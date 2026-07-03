using System.Diagnostics;

namespace FinanceTracker.Core.Services.Tracing;

/// <summary>
/// Central OpenTelemetry activity source for the FinanceTracker application.
/// Register via <c>AddSource("FinanceTracker")</c> in your OTEL configuration.
/// </summary>
public static class FinanceTrackerActivitySource
{
	public const string Name = "FinanceTracker";
	public static readonly ActivitySource Instance = new ActivitySource(name: Name);

	/// <summary>Standard operation names used as span names.</summary>
	public static class Operations
	{
		public const string EventStoreSave = "eventstore.save";
		public const string EventStoreLoad = "eventstore.load";
		public const string RabbitMqConsume = "rabbitmq.consume";
	}

	/// <summary>Standard span tag keys.</summary>
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

	/// <summary>W3C Trace Context header names for RabbitMQ propagation.</summary>
	public static class TraceContextHeaders
	{
		public const string TraceParent = "traceparent";
		public const string TraceState = "tracestate";
	}
}
