namespace FinanceTracker.Core.Exceptions;

public sealed class ActivatingException(string message) : Exception(message: message);