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
	ConsumerDeadLetter
}