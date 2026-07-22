namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class CannotDeleteSystemRoleException(string message = "System roles cannot be deleted.") : DomainException(message: message);
