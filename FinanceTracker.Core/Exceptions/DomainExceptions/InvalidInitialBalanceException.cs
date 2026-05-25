namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidInitialBalanceException(string message) : DomainException(message: message);
