using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.Behaviours.Authorization;

public interface IEntityLoader<in TRequest, TEntity, TError> 
	where TRequest : IAuthorizable
	where TError : AppException
{
	Task<Result<TEntity, TError>> LoadAsync(
		TRequest request,
		CancellationToken ct
	);
}
