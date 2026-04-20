namespace FinanceTracker.Core.Exceptions;

public sealed class UnknownEventException(string message, Type eventType) : Exception(message: message)
{
	public Type EventType { get; init; } = eventType;
}