namespace FinanceTracker.Core.Exceptions;

public abstract class AppException(string message) : Exception(message: message);
