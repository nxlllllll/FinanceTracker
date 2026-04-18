namespace FinanceTracker.Infrastructure.Database.EventStore;

public interface IEventTypeRegistry
{
	Type ResolveType(string typeName);
}