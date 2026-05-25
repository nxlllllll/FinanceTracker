namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class UnknownEventException(string message, Type eventType) : ConfigurationException(message: message)
{
	public Type EventType { get; init; } = eventType;
}
