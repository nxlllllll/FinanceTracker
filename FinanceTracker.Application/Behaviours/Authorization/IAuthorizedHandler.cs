using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Authorization;

public interface IAuthorizedHandler<in TRequest, in TEntity, TValue, TError>
	where TRequest : IRequest<Result<TValue, TError>>
	where TError : AppException
{
	Task<Result<TValue, TError>> HandleAsync(
		TRequest request,
		TEntity entity,
		CancellationToken ct
	);
}
