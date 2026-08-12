namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;

[ErrorCode(code: "concurrency.conflict")]
public sealed class ConcurrencyConflictException(string message, Guid id) : DomainException(message: message)
{
	public Guid Id { get; } = id;
}
