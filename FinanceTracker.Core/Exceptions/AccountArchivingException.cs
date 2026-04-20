namespace FinanceTracker.Core.Exceptions;

public sealed class AccountArchivingException(string message) : Exception(message: message);
