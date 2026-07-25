namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "validation.invalid_email")]
public sealed class EmailException(string message, string email) : DomainException(message: message)
{
	public string Email { get; init; } = email;
}
