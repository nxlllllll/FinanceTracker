namespace FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;

[ErrorCode(code: "category.depth_exceeded")]
public sealed class CategoryDepthExceededException(string message, int maxDepth) : DomainException(message: message)
{
	public int MaxDepth { get; } = maxDepth;
}
