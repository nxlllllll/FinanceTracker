namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;

[ErrorCode(code: "role.last_root_undeletable")]
public sealed class LastRootRoleException(string message = "Cannot remove the last remaining root user.") : DomainException(message: message);
