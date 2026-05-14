namespace FinanceTracker.Infrastructure.Database.EventStore;

public interface IEventTypeResolver
{
	Type ResolveType(string typeName);
	int GetCurrentVersion(string typeName);
}