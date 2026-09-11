namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;

[ErrorCode(code: "category.type_mismatch")]
public sealed class CategoryTypeMismatchException(string message) : DomainException(message: message);
