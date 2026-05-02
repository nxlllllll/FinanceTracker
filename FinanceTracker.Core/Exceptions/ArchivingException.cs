namespace FinanceTracker.Core.Exceptions;

public sealed class ArchivingException(string message) : DomainException(message: message);