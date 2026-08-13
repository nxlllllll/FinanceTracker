namespace FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;

/// <summary>
/// Raised when a client states an expected version that no longer matches
/// the stored one — the entity changed between the read and the write.
/// </summary>
[ErrorCode(code: "concurrency.precondition_failed")]
public sealed class PreconditionFailedException(
	string message,
	Guid? id,
	int expectedVersion,
	int actualVersion
) : DomainException(message: message)
{
	public Guid? Id { get; } = id;
	public int ExpectedVersion { get; } = expectedVersion;
	public int ActualVersion { get; } = actualVersion;
}
