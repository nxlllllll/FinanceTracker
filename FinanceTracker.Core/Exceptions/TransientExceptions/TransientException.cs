namespace FinanceTracker.Core.Exceptions.TransientExceptions;

/// <summary>
/// Base class for failures caused by data the system expects to have but does not have <i>yet</i>.
/// </summary>
public abstract class TransientException(string message, int retryAfterSeconds) : AppException(message: message)
{
	public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
