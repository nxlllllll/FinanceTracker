namespace FinanceTracker.Application.Behaviours.Idempotency;

/// <summary>
/// Marks a command as idempotent. Commands implementing this interface are intercepted
/// by <see cref="IdempotencyBehaviour{TRequest,TResponse}"/>, which caches the result
/// and returns it on duplicate submissions without re-executing the handler.
/// </summary>
public interface IIdempotentCommand
{
	/// <summary>
	/// Client-supplied key that uniquely identifies this command invocation.
	/// Must not be <see cref="Guid.Empty"/> — the behaviour will return an error if it is.
	/// </summary>
	Guid IdempotencyKey { get; }
}
