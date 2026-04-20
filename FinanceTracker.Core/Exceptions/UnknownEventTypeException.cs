namespace FinanceTracker.Core.Exceptions;

public sealed class UnknownEventTypeException(string message, string eventType) : Exception(message: message)
{
	public string EventType { get; init; } = eventType;
}