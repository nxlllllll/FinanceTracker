namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class InvalidTransferStatusException(string message) : DomainException(message: message);