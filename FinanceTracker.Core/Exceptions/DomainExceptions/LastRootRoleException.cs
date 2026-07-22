namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class LastRootRoleException(string message = "Cannot remove the last remaining root user.") : DomainException(message: message);
