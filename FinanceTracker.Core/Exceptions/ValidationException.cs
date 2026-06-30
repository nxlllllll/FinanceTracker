namespace FinanceTracker.Core.Exceptions;

/// <summary>
/// Thrown by <c>ValidationBehavior</c> when one or more FluentValidation
/// rules fail. Contains all validation error messages for the request.
/// </summary>
public sealed class ValidationException(IReadOnlyList<string> errors) : AppException(message: "One or more validation errors occurred.")
{
	/// <summary>The list of validation error messages from all failed validators.</summary>
	public IReadOnlyList<string> Errors { get; } = errors;
}