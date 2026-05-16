namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

public interface IIntegrationEventTypeResolver
{
	Type ResolveType(string eventType);
	string ResolveTypeName(Type eventType);
}