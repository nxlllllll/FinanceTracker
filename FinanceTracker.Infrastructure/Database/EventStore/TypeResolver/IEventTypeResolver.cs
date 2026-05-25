namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

public interface IEventTypeResolver
{
	Type ResolveType(string typeName);
	int GetCurrentVersion(string typeName);
}
