namespace FinanceTracker.Contracts.Messages;

public interface IHasEventTime
{
	DateTimeOffset OccurredAt { get; }
}
