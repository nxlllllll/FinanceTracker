namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;

/// <summary>
/// Raised when an operation carries a date earlier than the account it belongs to.
/// </summary>
[ErrorCode(code: "account.operation_predates_creation")]
public sealed class OperationPredatesAccountException(string message) : DomainException(message: message);
