namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class IncompatibleEventVersionException(
	string message,
	string eventType,
	int storedVersion,
	int currentVersion
) : ConfigurationException(message: message)
{
	public string EventType { get; init; } = eventType;
	public int StoredVersion { get; init; } = storedVersion;
	public int CurrentVersion { get; init; } = currentVersion;
}
