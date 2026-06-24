using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using MediatR;

namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// Handles a command or query after the associated entity has been loaded and
/// ownership verified by <c>IEntityLoader</c>.
/// Implement this instead of <see cref="IRequestHandler{TRequest,TResponse}"/> when
/// the use case requires an entity to be pre-loaded and access-checked.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TEntity">The pre-loaded entity (e.g. <c>Account</c>).</typeparam>
/// <typeparam name="TValue">The success value type of the result.</typeparam>
/// <typeparam name="TError">The domain exception type of the result.</typeparam>
public interface IAuthorizedHandler<in TRequest, in TEntity, TValue, TError>
	where TRequest : IRequest<Result<TValue, TError>>
	where TError : AppException
{
	/// <summary>
	/// Executes the use case with the pre-loaded, access-verified <paramref name="entity"/>.
	/// </summary>
	Task<Result<TValue, TError>> HandleAsync(
		TRequest request,
		TEntity accounts,
		CancellationToken ct
	);
}