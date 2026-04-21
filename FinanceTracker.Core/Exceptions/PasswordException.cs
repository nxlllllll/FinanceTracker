namespace FinanceTracker.Core.Exceptions;

public sealed class PasswordException(string message) : Exception(message: message);