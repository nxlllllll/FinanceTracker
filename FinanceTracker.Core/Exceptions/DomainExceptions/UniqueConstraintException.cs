namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "data.unique_constraint_violated")]
public sealed class UniqueConstraintException(string message, string constraintName) : DomainException(message: message)
{
	public string ConstraintName { get; } = constraintName;
}
