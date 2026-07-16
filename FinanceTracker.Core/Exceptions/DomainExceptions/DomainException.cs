namespace FinanceTracker.Core.Exceptions.DomainExceptions;

/// <summary>
/// Base class for domain rule violations. Returned via <c>Result.Failure</c> rather than thrown,
/// so they propagate through the pipeline without unwinding the call stack.
/// </summary>
public abstract class DomainException(string message) : AppException(message: message);
