namespace FinanceTracker.Application.Behaviours.Authorization;

/// <summary>
/// Marks a request as requiring entity ownership verification.
/// Implemented alongside <see cref="IAuthorizedHandler{TRequest,TEntity,TValue,TError}"/>
/// to ensure the requesting user owns the entity being operated on.
/// </summary>
public interface IAuthorizable
{
	/// <summary>ID of the user making the request. Compared against the entity's owner ID in the loader.</summary>
	Guid UserId { get; }
}
