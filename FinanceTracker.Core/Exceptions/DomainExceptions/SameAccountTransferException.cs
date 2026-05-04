namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class SameAccountTransferException(string message) : DomainException(message: message);