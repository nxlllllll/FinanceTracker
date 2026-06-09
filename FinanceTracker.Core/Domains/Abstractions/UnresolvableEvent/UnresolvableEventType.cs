namespace FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;

public enum UnresolvableEventType
{
	OutboxDeadLetter,
	TransferCompensation,
	ConsumerDeadLetter
}