namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ConcurrencyConflictException(string message, Guid id) : DomainException(message: message)
{
	public Guid Id { get; init; } = id;
}