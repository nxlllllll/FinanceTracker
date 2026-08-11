using System.Diagnostics.Metrics;

namespace FinanceTracker.Core.Observability.Metrics;

/// <summary>
/// Central OpenTelemetry metrics for cross-cutting application concerns, shared across
/// Core, Application, and Infrastructure. Mirrors <c>FinanceTrackerActivitySource</c>.
/// Register via <c>AddMeter("FinanceTracker")</c> in your OTEL configuration.
/// </summary>
public static class FinanceTrackerMetrics
{
	public const string MeterName = "FinanceTracker";

	private static readonly Meter Meter = new Meter(name: MeterName);

	/// <summary>
	/// Incremented every time <c>FallbackRateLimiter</c> falls back to the in-memory limiter
	/// because the Redis-backed limiter was unavailable or too slow to respond.
	/// A sustained non-zero rate indicates Redis is down or unreachable and should alert.
	/// </summary>
	public static readonly Counter<long> RateLimiterFallbackActivated = Meter.CreateCounter<long>(
		name: "ratelimiter.fallback.activated",
		description: "Total number of requests where the rate limiter fell back to in-memory because Redis was unavailable."
	);

	/// <summary>
	/// Incremented for every <c>IUnitOfWork.OnCommitted</c> callback that throws
	/// after its enclosing transaction already committed successfully
	/// </summary>
	public static readonly Counter<long> OnCommittedCallbackFailures = Meter.CreateCounter<long>(
		name: "unitofwork.oncommitted_callback.failures",
		description: "Total number of OnCommitted callbacks that threw after their transaction already committed successfully."
	);

	/// <summary>
	/// Refresh tokens presented after their session had already been revoked, tagged by outcome.
	/// </summary>
	public static readonly Counter<long> RefreshTokenReplay = Meter.CreateCounter<long>(
		name: "refresh_token.replay",
		description: "Refresh tokens presented after their session was revoked. Tagged by outcome (allowed/reuse_detected)."
	);

	/// <summary>
	/// Cache writes and deletes that never reached Redis because it was
	/// unavailable at that moment, tagged by operation.
	/// </summary>
	public static readonly Counter<long> CacheOperationFailures = Meter.CreateCounter<long>(
		name: "cache.operation.failures",
		description: "Cache operations that failed because Redis was unavailable. Tagged by operation (read/write/delete)."
	);

	/// <summary>
	/// Every command and query that reaches the pipeline, tagged by request type and outcome.
	/// </summary>
	public static readonly Counter<long> CommandExecuted = Meter.CreateCounter<long>(
		name: "command.executed",
		description: "Requests handled by the MediatR pipeline. Tagged by request_type and outcome (success/failure/error)."
	);

	/// <summary>
	/// How long the handler itself took, tagged by request type.
	/// </summary>
	public static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>(
		name: "command.duration",
		unit: "s",
		description: "Time spent inside the MediatR pipeline, excluding HTTP overhead. Tagged by request_type."
	);

	/// <summary>
	/// Retries performed by <c>RetryBehaviour</c>, tagged by what triggered them.
	/// </summary>
	public static readonly Counter<long> CommandRetried = Meter.CreateCounter<long>(
		name: "command.retried",
		description: "Retry attempts made by the pipeline. Tagged by request_type and reason (concurrency_conflict/transient_fault)."
	);

	/// <summary>
	/// Outcome of acquiring an idempotency key, tagged by kind.
	/// </summary>
	public static readonly Counter<long> IdempotencyAcquisition = Meter.CreateCounter<long>(
		name: "idempotency.acquisition",
		description: "Idempotency key acquisitions. Tagged by kind (cached_response/reserved/failed)."
	);

	/// <summary>Standard metric tag keys.</summary>
	public static class Tags
	{
		public const string Outcome = "outcome";
		public const string Operation = "operation";
		public const string RequestType = "request_type";
		public const string Reason = "reason";
		public const string Kind = "kind";
	}

	/// <summary>Values for the <see cref="Tags.Outcome"/> tag on <see cref="RefreshTokenReplay"/>.</summary>
	public static class ReplayOutcomes
	{
		/// <summary>Treated as a retry of a rotation whose response never arrived; a replacement was issued.</summary>
		public const string Allowed = "allowed";

		/// <summary>Treated as reuse of a stolen token; every session for the user was revoked.</summary>
		public const string ReuseDetected = "reuse_detected";
	}

	/// <summary>Values for the <see cref="Tags.Operation"/> tag on <see cref="CacheOperationFailures"/>.</summary>
	public static class CacheOperations
	{
		public const string Read = "read";
		public const string Write = "write";
		public const string Delete = "delete";
	}

	/// <summary>Values for the <see cref="Tags.Outcome"/> tag on <see cref="CommandExecuted"/>.</summary>
	public static class CommandOutcomes
	{
		/// <summary>Completed and returned a value.</summary>
		public const string Success = "success";

		/// <summary>Refused by a domain rule or by validation — the system working, not failing.</summary>
		public const string Failure = "failure";

		/// <summary>Threw. Something the domain did not anticipate.</summary>
		public const string Error = "error";
	}

	/// <summary>Values for the <see cref="Tags.Reason"/> tag on <see cref="CommandRetried"/>.</summary>
	public static class RetryReasons
	{
		public const string ConcurrencyConflict = "concurrency_conflict";
		public const string TransientFault = "transient_fault";
	}
}
