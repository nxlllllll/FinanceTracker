namespace FinanceTracker.Core.Domains.Abstractions.ES.Event;

public interface IEvent
{
	Guid Id { get; }
	DateTimeOffset OccurredAt { get; }
}
