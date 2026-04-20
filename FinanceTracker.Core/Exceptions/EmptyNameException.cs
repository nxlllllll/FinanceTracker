namespace FinanceTracker.Core.Exceptions;

public sealed class EmptyNameException(string message) : Exception(message: message);