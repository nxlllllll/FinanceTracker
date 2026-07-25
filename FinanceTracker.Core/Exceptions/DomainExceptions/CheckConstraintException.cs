namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "data.check_constraint_violated")]
public sealed class CheckConstraintException(string message, string constraintName) : DomainException(message: message)
{
	public string ConstraintName { get; } = constraintName;
}
