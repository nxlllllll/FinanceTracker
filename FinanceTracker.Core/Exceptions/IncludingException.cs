namespace FinanceTracker.Core.Exceptions;

public sealed class IncludingException(string message) : Exception(message: message);