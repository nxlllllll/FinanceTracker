using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Auth;
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

	private static UserSession CreateSuperseded(
		Guid successorId,
		TimeSpan revokedAgo
	) => UserSession.Reconstitute(
		id: Guid.CreateVersion7(),
		userId: Guid.CreateVersion7(),
		refreshTokenHash: "hash",
		expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: 1),
		createdAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: -10),
		revokedAt: FakeDateProvider.Default.UtcNow - revokedAgo,
		supersededBySessionId: successorId
	);

	private static UserSession CreateSuccessor(
		Guid id,
		DateTimeOffset? revokedAt = null,
		Guid? ownSuccessorId = null
	) => UserSession.Reconstitute(
		id: id,
		userId: Guid.CreateVersion7(),
		refreshTokenHash: "successor-hash",
		expiresAt: FakeDateProvider.Default.UtcNow.AddHours(hours: 1),
		createdAt: FakeDateProvider.Default.UtcNow,
		revokedAt: revokedAt,
		supersededBySessionId: ownSuccessorId
	);

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

	[Test]
	public async Task SupersedeBy_WhenActive_ShouldRevokeAndRecordTheSuccessor()
	{
		UserSession session = CreateActive();
		Guid successorId = Guid.CreateVersion7();
		DateTimeOffset revokedAt = FakeDateProvider.Default.UtcNow;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.SupersedeBy(
			successorSessionId: successorId,
			revokedAt: revokedAt
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: session.RevokedAt).IsEqualTo(expected: revokedAt);
		await Assert.That(value: session.SupersededBySessionId).IsEqualTo(expected: successorId);
	}

	[Test]
	public async Task SupersedeBy_WhenAlreadyRevoked_ShouldReturnFailureAndLeaveTheSuccessorUnset()
	{
		UserSession session = CreateRevoked();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.SupersedeBy(
			successorSessionId: Guid.CreateVersion7(),
			revokedAt: FakeDateProvider.Default.UtcNow
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();

		await Assert.That(value: session.SupersededBySessionId).IsNull();
	}

	[Test]
	public async Task SupersedeBy_WhenSuccessorIsItself_ShouldReturnFailure()
	{
		UserSession session = CreateActive();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = session.SupersedeBy(
			successorSessionId: session.Id,
			revokedAt: FakeDateProvider.Default.UtcNow
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: session.RevokedAt).IsNull();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenSuccessorIsAliveAndUnusedInsideTheWindow_ShouldBeTrue()
	{
		Guid successorId = Guid.CreateVersion7();
		UserSession session = CreateSuperseded(
			successorId: successorId,
			revokedAgo: TimeSpan.FromSeconds(value: 5)
		);

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: successorId),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsTrue();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenTheWindowHasPassed_ShouldBeFalse()
	{
		Guid successorId = Guid.CreateVersion7();
		UserSession session = CreateSuperseded(
			successorId: successorId,
			revokedAgo: TimeSpan.FromMinutes(value: 5)
		);

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: successorId),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenSuccessorWasRevoked_ShouldBeFalse()
	{
		Guid successorId = Guid.CreateVersion7();
		UserSession session = CreateSuperseded(
			successorId: successorId,
			revokedAgo: TimeSpan.FromSeconds(value: 5)
		);

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: successorId, revokedAt: FakeDateProvider.Default.UtcNow),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenSuccessorWasItselfRotated_ShouldBeFalse()
	{
		Guid successorId = Guid.CreateVersion7();
		UserSession session = CreateSuperseded(
			successorId: successorId,
			revokedAgo: TimeSpan.FromSeconds(value: 5)
		);

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: successorId, ownSuccessorId: Guid.CreateVersion7()),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenGivenAnUnrelatedSession_ShouldBeFalse()
	{
		UserSession session = CreateSuperseded(
			successorId: Guid.CreateVersion7(),
			revokedAgo: TimeSpan.FromSeconds(value: 5)
		);

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: Guid.CreateVersion7()),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsFalse();
	}

	[Test]
	public async Task WasSupersededByUnusedSession_WhenRevokedWithoutASuccessor_ShouldBeFalse()
	{
		UserSession session = CreateRevoked();

		bool result = session.WasSupersededByUnusedSession(
			successor: CreateSuccessor(id: Guid.CreateVersion7()),
			now: FakeDateProvider.Default.UtcNow,
			graceWindow: TimeSpan.FromSeconds(value: 30)
		);

		await Assert.That(value: result).IsFalse();
	}
}
