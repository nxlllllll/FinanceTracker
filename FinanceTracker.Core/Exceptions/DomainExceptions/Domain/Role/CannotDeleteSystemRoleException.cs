namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Role;

[ErrorCode(code: "role.system_role_undeletable")]
public sealed class CannotDeleteSystemRoleException(string message = "System roles cannot be deleted.") : DomainException(message: message);
