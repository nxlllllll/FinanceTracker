using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Core.Domains.User;

public sealed class UserSession
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public string RefreshTokenHash { get; private set; } = string.Empty;
	public DateTimeOffset ExpiresAt { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }
	public DateTimeOffset? RevokedAt { get; private set; }

	public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

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

	public Result<Unit, DomainException> Revoke(DateTimeOffset revokedAt)
	{
		if (!IsActive)
			return Result<Unit, DomainException>.Failure(error: new InvalidTokenException(message: "Session is already revoked or expired."));

		RevokedAt = revokedAt;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}
