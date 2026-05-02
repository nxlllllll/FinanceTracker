namespace FinanceTracker.Core.Exceptions;

public sealed class InvalidInitialBalanceException(string message) : DomainException(message: message);