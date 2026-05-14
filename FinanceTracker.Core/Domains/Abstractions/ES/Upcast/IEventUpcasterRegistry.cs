using System.Text.Json;

namespace FinanceTracker.Core.Domains.Abstractions.ES.Upcast;

public interface IEventUpcasterRegistry
{
	JsonDocument Apply(
		string eventType,
		JsonDocument source,
		int storedVersion,
		int currentVersion
	);
}