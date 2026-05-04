namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ArchivingException(string message) : DomainException(message: message);