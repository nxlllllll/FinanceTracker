namespace FinanceTracker.Core.Exceptions;

public sealed class AccountNotFoundException(string message, Guid accountId) : Exception(message: message)
{
	public Guid AccountId { get; init; } = accountId;
}