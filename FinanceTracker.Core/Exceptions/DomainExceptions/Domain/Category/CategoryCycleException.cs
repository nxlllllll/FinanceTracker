namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;

[ErrorCode(code: "category.cycle")]
public sealed class CategoryCycleException(string message) : DomainException(message: message);
