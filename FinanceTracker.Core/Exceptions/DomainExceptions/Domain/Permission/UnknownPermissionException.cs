namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Permission;

[ErrorCode(code: "permission.unknown")]
public sealed class UnknownPermissionException(string message) : DomainException(message: message);
