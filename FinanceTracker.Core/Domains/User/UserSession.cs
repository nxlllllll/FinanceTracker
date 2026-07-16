using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Domains.User;

/// <summary>
/// Represents an active user authentication session backed by a refresh token.
/// Sessions are invalidated either by explicit <see cref="Revoke"/> or by expiry.
/// </summary>
public sealed class UserSession
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	/// <summary>Hashed refresh token stored for secure comparison on refresh requests.</summary>
	public string RefreshTokenHash { get; private set; } = String.Empty;
	/// <summary>UTC expiry after which the session is no longer valid.</summary>
	public DateTimeOffset ExpiresAt { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }
	/// <summary>UTC timestamp of explicit revocation. <c>null</c> if still active.</summary>
	public DateTimeOffset? RevokedAt { get; private set; }

	private UserSession() { }

	public static UserSession Create(
		Guid userId,
		string refreshTokenHash,
		DateTimeOffset expiresAt,
		DateTimeOffset createdAt)
	{
		return new UserSession
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			RefreshTokenHash = refreshTokenHash,
			ExpiresAt = expiresAt,
			CreatedAt = createdAt
		};
	}

	public static UserSession Reconstitute(
		Guid id,
		Guid userId,
		string refreshTokenHash,
		DateTimeOffset expiresAt,
		DateTimeOffset createdAt,
		DateTimeOffset? revokedAt)
	{
		return new UserSession
		{
			Id = id,
			UserId = userId,
			RefreshTokenHash = refreshTokenHash,
			ExpiresAt = expiresAt,
			CreatedAt = createdAt,
			RevokedAt = revokedAt
		};
	}

	/// <summary>Revokes the session. Fails if already revoked or expired.</summary>
	public Result<Unit, DomainException> Revoke(DateTimeOffset revokedAt)
	{
		if (!IsActive(now: revokedAt))
			return Result<Unit, DomainException>.Failure(error: new InvalidTokenException(message: "Session is already revoked or expired."));

		RevokedAt = revokedAt;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>Returns <c>true</c> if the session has not been revoked and has not yet expired.</summary>
	public bool IsActive(DateTimeOffset now)
		=> RevokedAt is null && now < ExpiresAt;

}
