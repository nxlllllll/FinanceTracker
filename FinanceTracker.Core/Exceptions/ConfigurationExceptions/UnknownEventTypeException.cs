namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class UnknownEventTypeException(string message, List<string> eventTypes) : ConfigurationException(message: message)
{
	public List<string> EventTypes { get; init; } = eventTypes;
}
