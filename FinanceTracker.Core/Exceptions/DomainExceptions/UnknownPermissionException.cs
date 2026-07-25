namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "permission.unknown")]
public sealed class UnknownPermissionException(string message) : DomainException(message: message);
