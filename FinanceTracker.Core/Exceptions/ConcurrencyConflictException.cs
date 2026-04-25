namespace FinanceTracker.Core.Exceptions;

public sealed class ConcurrencyConflictException(string message, Guid id) : Exception(message: message)
{
	public Guid Id { get; init; } = id;
}