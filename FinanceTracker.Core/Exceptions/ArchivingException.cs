namespace FinanceTracker.Core.Exceptions;

public sealed class ArchivingException(string message) : Exception(message: message);