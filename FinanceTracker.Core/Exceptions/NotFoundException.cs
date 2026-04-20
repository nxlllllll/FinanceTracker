namespace FinanceTracker.Core.Exceptions;

public sealed class NotFoundException(string message, Guid id) : Exception(message: message)
{
	public Guid Id { get; init; } = id;
}