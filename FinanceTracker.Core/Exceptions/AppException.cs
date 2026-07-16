namespace FinanceTracker.Core.Exceptions;

/// <summary>
/// Base class for all application exceptions. Separates expected domain and
/// validation errors (subclasses of this) from unexpected infrastructure exceptions,
/// enabling consistent error handling at the pipeline and API boundary.
/// </summary>
public abstract class AppException(string message) : Exception(message: message);
