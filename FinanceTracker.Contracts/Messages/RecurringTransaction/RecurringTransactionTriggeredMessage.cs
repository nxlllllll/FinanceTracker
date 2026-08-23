using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.RecurringTransaction;

/// <summary>
/// Published by <c>RecurringTransactionHandlingJob</c> when a recurring transaction is due.
/// Consumed by the recurring transaction projection worker to create the actual transaction record.
/// <para>
/// The <see cref="IRoutableMessage.MessageId"/> is a <c>DeterministicGuid</c> derived from the
/// recurring transaction ID and the instant that occurrence was due, guaranteeing at-most-once
/// processing per occurrence regardless of how the calendar is divided or how late the job runs.
/// </para>
/// </summary>
public sealed record RecurringTransactionTriggeredMessage(
	Guid MessageId,
	Guid RecurringTransactionId,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	decimal Amount,
	string Currency,
	string Direction,
	string? Description,
	DateTimeOffset OccurredAt,
	Guid CorrelationId
) : IRoutableMessage
{
	/// <inheritdoc/>
	public string RoutingKey => AggregateTypeNames.RecurringTransaction;
}
