using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
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
	public Guid? SupersededBySessionId { get; private set; }

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
		DateTimeOffset? revokedAt,
		Guid? supersededBySessionId)
	{
		return new UserSession
		{
			Id = id,
			UserId = userId,
			RefreshTokenHash = refreshTokenHash,
			ExpiresAt = expiresAt,
			CreatedAt = createdAt,
			RevokedAt = revokedAt,
			SupersededBySessionId = supersededBySessionId
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

	/// <summary>
	/// Revokes the session and records the one that replaced it.
	/// Fails if already revoked or expired.
	/// </summary>
	public Result<Unit, DomainException> SupersedeBy(Guid successorSessionId, DateTimeOffset revokedAt)
	{
		if (successorSessionId == Id)
			return Result<Unit, DomainException>.Failure(error: new InvalidTokenException(message: "A session cannot supersede itself."));

		Result<Unit, DomainException> revokeResult = Revoke(revokedAt: revokedAt);
		if (revokeResult.IsFailure)
			return revokeResult;

		SupersededBySessionId = successorSessionId;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}

	/// <summary>Returns <c>true</c> if the session has not been revoked and has not yet expired.</summary>
	public bool IsActive(DateTimeOffset now)
		=> RevokedAt is null && now < ExpiresAt;

	/// <summary>
	/// Returns <c>true</c> if this session was replaced by a rotation and the replacement has not
	/// itself been rotated — meaning the client never presented the token it was given.
	/// </summary>
	public bool WasSupersededByUnusedSession(UserSession successor, DateTimeOffset now, TimeSpan graceWindow)
	{
		if (SupersededBySessionId != successor.Id)
			return false;

		if (RevokedAt is not { } revokedAt || now - revokedAt > graceWindow)
			return false;

		return successor.IsActive(now: now) && successor.SupersededBySessionId is null;
	}
}
