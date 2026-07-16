namespace FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

/// <summary>
/// Classifies why an event could not be processed and was moved to the
/// <c>unresolvable_events</c> table for manual investigation.
/// </summary>
public enum UnresolvableEventType
{
	/// <summary>
	/// An outbox message exceeded the maximum retry count and could not be
	/// published to RabbitMQ. The aggregate events are preserved in the outbox table.
	/// </summary>
	OutboxDeadLetter,

	/// <summary>
	/// A transfer compensation (refund to the source account) failed and could not
	/// be completed automatically. Manual intervention is required to restore the balance.
	/// </summary>
	TransferCompensation,

	/// <summary>
	/// A RabbitMQ consumer exceeded the maximum retry count for a message.
	/// The message was sent to the dead-letter exchange and recorded here for diagnostics.
	/// </summary>
	ConsumerDeadLetter,

	/// <summary>
	/// A recurring transaction occurrence could not be turned into an actual transaction —
	/// either a data-integrity problem (missing account, invalid message data) or a domain
	/// rule rejection (e.g. insufficient funds). The occurrence for this period is permanently
	/// skipped; it won't be retried, since none of these causes resolve themselves on retry.
	/// </summary>
	RecurringTransactionFailed,

	/// <summary>
	/// A mandatory publish could not be routed to any queue by the broker (misconfigured
	/// routing key, unbound exchange) and was returned via RabbitMQ's basic.return.
	/// </summary>
	PublisherUnroutable,

	/// <summary>
	/// A pending exchange rate could not be settled automatically: the real rate arrived, but the
	/// correction was rejected — typically because the account was archived while the rate was still
	/// pending, and an archived account refuses balance adjustments.
	/// </summary>
	RateAdjustmentFailed
}
