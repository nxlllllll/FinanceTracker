using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class UserSessionTests
{
	private static UserSession CreateActive()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			supersededBySessionId: null,
			refreshTokenHash: "hash",
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: 1),
			createdAt: FakeDateProvider.Default.UtcNow,
			revokedAt: null
		);
	}

	private static UserSession CreateExpired()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			supersededBySessionId: null,
			refreshTokenHash: "hash",
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: -1),
			createdAt: FakeDateProvider.Default.UtcNow.AddHours(hours: -2),
			revokedAt: null
		);
	}

	private static UserSession CreateRevoked()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			supersededBySessionId: null,
			refreshTokenHash: "hash",
			expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: 1),
			createdAt: FakeDateProvider.Default.UtcNow,
			revokedAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: -5)
		);
	}

	[Test]
	public async Task IsActive_WhenNotRevokedAndNotExpired_ShouldBeTrue()
	{
		UserSession session = CreateActive();
		bool result = session.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task IsActive_WhenExpired_ShouldBeFalse()
	{
		UserSession session = CreateExpired();
		bool result = session.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task IsActive_WhenRevoked_ShouldBeFalse()
	{
		UserSession session = CreateRevoked();
		bool result = session.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task Create_ShouldSetCorrectProperties()
	{
		Guid userId = Guid.CreateVersion7();
		DateTimeOffset expiresAt = FakeDateProvider.Default.UtcNow.AddDays(days: 7);
		DateTimeOffset createdAt = FakeDateProvider.Default.UtcNow;

		UserSession session = UserSession.Create(
			userId: userId,
			refreshTokenHash: "testhash",
			expiresAt: expiresAt,
			createdAt: createdAt
		);
		bool result = session.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: session.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: session.RefreshTokenHash).IsEqualTo(expected: "testhash");
		await Assert.That(value: session.ExpiresAt).IsEqualTo(expected: expiresAt);
		await Assert.That(value: session.RevokedAt).IsNull();
		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task Revoke_WhenActive_ShouldSetRevokedAt()
	{
		UserSession session = CreateActive();
		DateTimeOffset revokedAt = FakeDateProvider.Default.UtcNow;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: revokedAt);
		bool isActive = session.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: session.RevokedAt).IsEqualTo(expected: revokedAt);
		await Assert.That(value: isActive).IsFalse();
	}

	[Test]
	public async Task Revoke_WhenAlreadyRevoked_ShouldReturnFailure()
	{
		UserSession session = CreateRevoked();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Revoke_WhenExpired_ShouldReturnFailure()
	{
		UserSession session = CreateExpired();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.Revoke(revokedAt: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}
}
