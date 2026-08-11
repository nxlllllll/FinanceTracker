using System.Diagnostics;

namespace FinanceTracker.Core.Observability.Tracing;

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
		public const string OutboxPublish = "outbox.publish";
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
		public const string CorrelationId = "correlation.id";
	}

	/// <summary>W3C Trace Context header names for RabbitMQ propagation.</summary>
	public static class TraceContextHeaders
	{
		public const string TraceParent = "traceparent";
		public const string TraceState = "tracestate";
	}

	public static string? CaptureTraceParent()
	{
		if (Activity.Current is not { } current)
			return null;

		string flags = current.ActivityTraceFlags.HasFlag(flag: ActivityTraceFlags.Recorded) ? "01" : "00";

		return $"00-{current.TraceId}-{current.SpanId}-{flags}";
	}

	/// <summary>
	/// Parses a W3C <c>traceparent</c> into an <see cref="ActivityContext"/>.
	/// Returns <c>default</c> when the value is absent or malformed.
	/// </summary>
	public static ActivityContext ParseTraceParent(string? traceParent, string? traceState = null)
	{
		if (String.IsNullOrWhiteSpace(value: traceParent))
			return default;

		string[] parts = traceParent.Split(separator: '-');
		if (parts.Length != 4)
			return default;

		try
		{
			return new ActivityContext(
				traceId: ActivityTraceId.CreateFromString(idData: parts[1]),
				spanId: ActivitySpanId.CreateFromString(idData: parts[2]),
				traceFlags: parts[3] == "01" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None,
				traceState: traceState,
				isRemote: true
			);
		}
		catch
		{
			return default;
		}
	}
}
