namespace FinanceTracker.Core.Exceptions;

public sealed class DeactivatingException(string message) : Exception(message: message);