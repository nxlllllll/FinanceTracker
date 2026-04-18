namespace FinanceTracker.Core.Domains.Abstractions;

public interface IEvent
{
	Guid Id { get; }
	DateTime OccurredAt { get; }
}