using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class UserSessionTests
{
	private static UserSession CreateActive()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			refreshTokenHash: "hash",
			expiresAt: DateTime.UtcNow.AddHours(value: 1),
			createdAt: DateTime.UtcNow,
			revokedAt: null
		);
	}

	private static UserSession CreateExpired()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			refreshTokenHash: "hash",
			expiresAt: DateTime.UtcNow.AddHours(value: -1),
			createdAt: DateTime.UtcNow.AddHours(value: -2),
			revokedAt: null
		);
	}

	private static UserSession CreateRevoked()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			refreshTokenHash: "hash",
			expiresAt: DateTime.UtcNow.AddHours(value: 1),
			createdAt: DateTime.UtcNow,
			revokedAt: DateTime.UtcNow.AddMinutes(value: -5)
		);
	}

	[Test]
	public async Task IsActive_WhenNotRevokedAndNotExpired_ShouldBeTrue()
	{
		UserSession session = CreateActive();
		await Assert.That(value: session.IsActive).IsTrue();
	}

	[Test]
	public async Task IsActive_WhenExpired_ShouldBeFalse()
	{
		UserSession session = CreateExpired();
		await Assert.That(value: session.IsActive).IsFalse();
	}

	[Test]
	public async Task IsActive_WhenRevoked_ShouldBeFalse()
	{
		UserSession session = CreateRevoked();
		await Assert.That(value: session.IsActive).IsFalse();
	}

	[Test]
	public async Task Create_ShouldSetCorrectProperties()
	{
		Guid userId = Guid.CreateVersion7();
		DateTime expiresAt = DateTime.UtcNow.AddDays(value: 7);
		DateTime createdAt = DateTime.UtcNow;

		UserSession session = UserSession.Create(
			userId: userId,
			refreshTokenHash: "testhash",
			expiresAt: expiresAt,
			createdAt: createdAt
		);

		await Assert.That(value: session.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: session.RefreshTokenHash).IsEqualTo(expected: "testhash");
		await Assert.That(value: session.ExpiresAt).IsEqualTo(expected: expiresAt);
		await Assert.That(value: session.RevokedAt).IsNull();
		await Assert.That(value: session.IsActive).IsTrue();
	}

	[Test]
	public async Task Revoke_WhenActive_ShouldSetRevokedAt()
	{
		UserSession session = CreateActive();
		DateTime revokedAt = DateTime.UtcNow;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: revokedAt);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: session.RevokedAt).IsEqualTo(expected: revokedAt);
		await Assert.That(value: session.IsActive).IsFalse();
	}

	[Test]
	public async Task Revoke_WhenAlreadyRevoked_ShouldReturnFailure()
	{
		UserSession session = CreateRevoked();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: DateTime.UtcNow);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Revoke_WhenExpired_ShouldReturnFailure()
	{
		UserSession session = CreateExpired();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: DateTime.UtcNow);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}
}