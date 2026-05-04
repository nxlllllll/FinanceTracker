using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Results;

public interface IResult<TSelf, TError>
	where TSelf : IResult<TSelf, TError>
	where TError : AppException
{
	static abstract TSelf CreateFailure(TError error);
}