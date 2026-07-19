namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class SelfPermissionModificationException(string message = "You cannot modify your own permissions.") : DomainException(message: message);
