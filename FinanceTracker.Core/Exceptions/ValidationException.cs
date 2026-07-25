namespace FinanceTracker.Core.Exceptions;

/// <summary>
/// Thrown by <c>ValidationBehavior</c> when one or more FluentValidation rules fail.
/// </summary>
[ErrorCode(code: "validation.failed")]
public sealed class ValidationException(
	IReadOnlyDictionary<string, string[]> errors
) : AppException(message: "One or more validation errors occurred.")
{
	/// <summary>Validation errors keyed by property name.</summary>
	public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
