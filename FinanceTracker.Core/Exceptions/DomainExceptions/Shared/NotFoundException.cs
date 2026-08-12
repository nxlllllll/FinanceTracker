namespace FinanceTracker.Core.Exceptions.DomainExceptions.Shared;

[ErrorCode(code: "resource.not_found")]
public sealed class NotFoundException(string message, Guid id) : DomainException(message: message)
{
	public Guid Id { get; init; } = id;
}
