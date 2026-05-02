namespace FinanceTracker.Core.Exceptions;

public sealed class NotFoundException(string message, Guid id) : DomainException(message: message)
{
	public Guid Id { get; init; } = id;
}