namespace FinanceTracker.Core.Exceptions.DomainExceptions;

public sealed class ActivatingException(
	string message
) : DomainException(message: message);