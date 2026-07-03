using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Results;

/// <summary>
/// Discriminated union representing either a successful value or a domain error.
/// Used as the return type of all domain operations and command handlers instead of throwing exceptions.
/// </summary>
/// <typeparam name="TValue">The success value type.</typeparam>
/// <typeparam name="TError">The domain exception type returned on failure.</typeparam>
public readonly struct Result<TValue, TError> : IResult, IResult<Result<TValue, TError>, TError>
	where TError : AppException
{
	private readonly TValue? _value;
	private readonly TError? _error;

	/// <summary><c>true</c> if the operation succeeded.</summary>
	public bool IsSuccess { get; }

	/// <summary><c>true</c> if the operation failed.</summary>
	public bool IsFailure => !IsSuccess;

	/// <summary>The success value. Returns <c>default</c> if <see cref="IsFailure"/>.</summary>
	public TValue? Value => IsSuccess ? _value : default;

	/// <summary>The domain error. Returns <c>default</c> if <see cref="IsSuccess"/>.</summary>
	public TError? Error => IsFailure ? _error : default;

	private Result(TValue value)
	{
		_value = value;
		IsSuccess = true;
	}

	private Result(TError error)
	{
		_error = error;
		IsSuccess = false;
	}

	/// <summary>Creates a successful result wrapping <paramref name="value"/>.</summary>
	public static Result<TValue, TError> Success(TValue value)
		=> new Result<TValue, TError>(value: value);

	/// <summary>Creates a failed result wrapping <paramref name="error"/>.</summary>
	public static Result<TValue, TError> Failure(TError error)
		=> new Result<TValue, TError>(error: error);

	/// <inheritdoc/>
	static Result<TValue, TError> IResult<Result<TValue, TError>, TError>.CreateFailure(TError error)
		=> Failure(error: error);

	/// <summary>
	/// Pattern-matches on the result, invoking <paramref name="onSuccess"/> or
	/// <paramref name="onFailure"/> depending on the outcome.
	/// </summary>
	public TResult Match<TResult>(
		Func<TValue, TResult> onSuccess,
		Func<TError, TResult> onFailure
	) => IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}
