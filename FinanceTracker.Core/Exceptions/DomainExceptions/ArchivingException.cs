namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "archiving.blocked")]
public sealed class ArchivingException(string message) : DomainException(message: message);
