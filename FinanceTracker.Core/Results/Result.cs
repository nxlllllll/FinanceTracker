using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Results;

public readonly struct Result<TValue, TError> : IResult<Result<TValue, TError>, TError> where TError : AppException
{
	private readonly TValue? _value;
	private readonly TError? _error;

	public bool IsSuccess { get; }
	public bool IsFailure => !IsSuccess;
	public TValue? Value => IsSuccess ? _value : default;
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

	public static Result<TValue, TError> Success(TValue value)
		=> new Result<TValue, TError>(value: value);

	public static Result<TValue, TError> Failure(TError error)
		=> new Result<TValue, TError>(error: error);

	static Result<TValue, TError> IResult<Result<TValue, TError>, TError>.CreateFailure(TError error)
		=> Failure(error: error);

	public TResult Match<TResult>(
		Func<TValue, TResult> onSuccess,
		Func<TError, TResult> onFailure
	) => IsSuccess ? onSuccess(_value!) : onFailure(_error!);
}