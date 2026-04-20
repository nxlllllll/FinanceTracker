namespace FinanceTracker.Core.Exceptions;

public sealed class AccountUnarchivingException(string message) : Exception(message: message);