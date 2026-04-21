namespace FinanceTracker.Core.Exceptions;

public sealed class ExcludingException(string message) : Exception(message: message);