namespace FinanceTracker.Core.Exceptions;

public sealed class ValidationException(IReadOnlyList<string> errors) : AppException(message: "One or more validation errors occurred.")
{
	public IReadOnlyList<string> Errors { get; } = errors;
}