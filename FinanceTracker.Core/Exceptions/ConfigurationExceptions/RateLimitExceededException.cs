namespace FinanceTracker.Core.Exceptions.ConfigurationExceptions;

public sealed class RateLimitExceededException(string commandName) : AppException(message: $"Rate limit exceeded for command '{commandName}'.");