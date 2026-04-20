namespace FinanceTracker.Core.Exceptions;

public sealed class UnarchivingException(string message) : Exception(message: message);