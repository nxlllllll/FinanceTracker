namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;

[ErrorCode(code: "permission.self_modification_denied")]
public sealed class SelfPermissionModificationException(string message = "You cannot modify your own permissions.") : DomainException(message: message);
