namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "rate_limit.exceeded")]
public sealed class RateLimitExceededException(
	string commandName,
	int retryAfterSeconds
) : DomainException(message: $"Rate limit exceeded for command '{commandName}'.")
{
	/// <summary>Seconds until the oldest request in the current window expires and a retry would be admitted.</summary>
	public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
