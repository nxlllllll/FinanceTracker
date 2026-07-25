namespace FinanceTracker.Core.Exceptions.DomainExceptions;

[ErrorCode(code: "account.invalid_initial_balance")]
public sealed class InvalidInitialBalanceException(string message) : DomainException(message: message);
