namespace FinanceTracker.Core.Repositories.Outbox;

/// <summary>
/// Represents an outbox message that has permanently failed after exceeding
/// the maximum retry count. Used by <c>DeadLetterMonitoringJob</c> for reporting.
/// </summary>
/// <param name="RetryCount">Number of failed publish attempts before moving to dead letter.</param>
/// <param name="FailedAt">UTC timestamp when the message was declared unresolvable.</param>
public sealed record DeadLetterMessage(
	Guid Id,
	Guid AggregateId,
	string AggregateType,
	int RetryCount,
	DateTimeOffset FailedAt
);
