namespace FinanceTracker.Core.Exceptions;

public sealed class UnarchivingException(string message) : DomainException(message: message);