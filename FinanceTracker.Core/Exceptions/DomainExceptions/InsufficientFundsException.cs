using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InsufficientFundsException(string message, Money balance) : DomainException(message: message)
{
	public Money Balance { get; } = balance;
}