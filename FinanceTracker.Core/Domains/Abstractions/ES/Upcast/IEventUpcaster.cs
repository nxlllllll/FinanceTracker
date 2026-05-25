using System.Text.Json;

namespace FinanceTracker.Core.Domains.Abstractions.ES.Upcast;

public interface IEventUpcaster
{
	string EventType { get; }
 
	int FromVersion { get; }
 
	int ToVersion { get; }
 
	JsonDocument Upcast(JsonDocument source);
}
