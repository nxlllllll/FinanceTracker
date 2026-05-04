namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class EmailException(string message, string email) : DomainException(message: message)
{
	public string Email { get; init; } = email;
}