namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class UniqueConstraintException(string message, string constraintName) : DomainException(message: message)
{
	public string ConstraintName { get; } = constraintName;
}
