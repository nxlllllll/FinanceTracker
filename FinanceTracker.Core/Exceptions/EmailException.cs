namespace FinanceTracker.Core.Exceptions;

public sealed class EmailException(string message, string email) : Exception(message: message)
{
	public string Email { get; init; } = email;
}