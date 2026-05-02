namespace FinanceTracker.Core.Exceptions;

public sealed class UnknownEventTypeException(string message, string eventType) : DomainException(message: message)
{
	public string EventType { get; init; } = eventType;
}