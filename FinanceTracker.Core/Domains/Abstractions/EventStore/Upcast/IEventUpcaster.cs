namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

public interface IEventUpcaster
{
	string EventType { get; }
	int FromVersion { get; }
	int ToVersion { get; }
	public Type FromType { get; }
	public Type ToType { get; }
	object Upcast(object source);
}