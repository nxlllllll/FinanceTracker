using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// Loads an entity and verifies that the requesting user has access to it.
/// Called by <see cref="AuthorizedHandlerAdapter{TRequest,TEntity,TValue,TError}"/>
/// before delegating to <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/>.
/// Return <c>Result.Failure</c> to deny access without throwing.
/// </summary>
public interface IEntityLoader<in TRequest, TEntity, TError>
	where TRequest : IAuthorizable
	where TError : AppException
{
	/// <summary>
	/// Loads the entity identified by <paramref name="request"/> and checks ownership.
	/// Returns <c>Result.Failure</c> with a <c>NotFoundException</c> or <c>ForbiddenException</c>
	/// if the entity does not exist or the user is not authorized.
	/// </summary>
	Task<Result<TEntity, TError>> LoadAsync(
		TRequest request,
		CancellationToken ct
	);
}

public interface IEntityLoader<in TRequest, TError>
	where TRequest : IAuthorizable
	where TError : AppException
{
	Task<Result<Unit, TError>> LoadAsync(
		TRequest request,
		CancellationToken ct
	);
}
