using FinanceTracker.Core.Domains.Abstractions.Aggregate;

namespace FinanceTracker.Contracts.Messages.RecurringTransaction;

/// <summary>
/// Published by <c>RecurringTransactionHandlingJob</c> when a recurring transaction is due.
/// Consumed by the recurring transaction projection worker to create the actual transaction record.
/// <para>
/// The <see cref="IRoutableMessage.MessageId"/> is a <c>DeterministicGuid</c> derived from
/// the recurring transaction ID and the current year/month — guaranteeing at-most-once
/// processing per calendar month even on retry.
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
