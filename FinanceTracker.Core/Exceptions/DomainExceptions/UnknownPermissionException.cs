namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class UnknownPermissionException(string message) : DomainException(message: message);
