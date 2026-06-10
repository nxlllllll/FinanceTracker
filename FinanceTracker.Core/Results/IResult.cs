using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Results;

/// <summary>
/// Non-generic marker interface for result types. Used by the MediatR pipeline
/// to detect whether a response is a result without knowing the type parameters.
/// </summary>
public interface IResult
{
	bool IsSuccess { get; }
	bool IsFailure { get; }
}

/// <summary>
/// Generic interface required by <c>IdempotencyBehavior</c> to create a failure result
/// from a cached error without knowing the concrete <c>Result</c> type.
/// </summary>
public interface IResult<out TSelf, in TError>
	where TSelf : IResult<TSelf, TError>
	where TError : AppException
{
	/// <summary>Factory method to create a failure result of type <typeparamref name="TSelf"/>.</summary>
	static abstract TSelf CreateFailure(TError error);
}