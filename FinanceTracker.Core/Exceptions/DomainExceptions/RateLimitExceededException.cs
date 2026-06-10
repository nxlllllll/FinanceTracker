namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class RateLimitExceededException(string commandName) : DomainException(message: $"Rate limit exceeded for command '{commandName}'.");
