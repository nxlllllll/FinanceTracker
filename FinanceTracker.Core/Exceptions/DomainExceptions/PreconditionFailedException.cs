namespace FinanceTracker.Core.Exceptions.DomainExceptions;

/// <summary>
/// The client supplied an expected version that no longer matches the entity's
/// current version — the resource was modified since the client last saw it.
/// </summary>
[ErrorCode(code: "concurrency.precondition_failed")]
public sealed class PreconditionFailedException(
	string message,
	Guid id,
	int expectedVersion,
	int actualVersion
) : DomainException(message: message)
{
	public Guid Id { get; } = id;
	public int ExpectedVersion { get; } = expectedVersion;
	public int ActualVersion { get; } = actualVersion;
}
