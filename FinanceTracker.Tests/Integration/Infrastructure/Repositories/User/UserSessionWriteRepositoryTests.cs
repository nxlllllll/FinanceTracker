using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class UserSessionWriteRepositoryTests : DatabaseFixture
{
	private UserSessionWriteRepository _userSessionWriteRepository = null!;
	private UserSessionReadRepository _userSessionReadRepository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_userSessionReadRepository = new UserSessionReadRepository(context: Context);
		_userSessionWriteRepository = new UserSessionWriteRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<Core.Domains.User.UserSession> CreateAndPersistSessionAsync(
		Guid userId,
		string hash = "default-hash",
		DateTimeOffset? expiresAt = null,
		DateTimeOffset? revokedAt = null)
	{
		Core.Domains.User.UserSession session = Core.Domains.User.UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: userId,
			refreshTokenHash: hash,
			expiresAt: expiresAt ?? DateTimeOffset.UtcNow.AddDays(days: 7),
			createdAt: FakeDateProvider.Default.UtcNow,
			revokedAt: revokedAt
		);

		await _userSessionWriteRepository.CreateAsync(session: session);
		await Context.SaveChangesAsync();
		return session;
	}

	[Test]
	public async Task CreateAsync_ShouldPersistSession()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session = Core.Domains.User.UserSession.Create(
			userId: userId,
			refreshTokenHash: "testhash",
			expiresAt: DateTimeOffset.UtcNow.AddDays(days: 7),
			createdAt: FakeDateProvider.Default.UtcNow
		);

		await _userSessionWriteRepository.CreateAsync(session: session);
		await Context.SaveChangesAsync();

		Core.Domains.User.UserSession? loaded = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "testhash");

		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.Id).IsEqualTo(expected: session.Id);
		await Assert.That(value: loaded.UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task RevokeAsync_ShouldSetRevokedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session = await CreateAndPersistSessionAsync(userId: userId, hash: "revoke-test-hash");
		DateTimeOffset revokedAt = FakeDateProvider.Default.UtcNow;

		await _userSessionWriteRepository.RevokeAsync(sessionId: session.Id, revokedAt: revokedAt);

		Core.Domains.User.UserSession? revoked = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "revoke-test-hash");
		bool? isActive = revoked?.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: revoked!.RevokedAt).IsNotNull();
		await Assert.That(value: isActive).IsFalse();
	}

	[Test]
	public async Task RevokeAsync_ShouldNotAffectOtherSessions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session1 = await CreateAndPersistSessionAsync(userId: userId, hash: "hash-1");
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "hash-2");

		await _userSessionWriteRepository.RevokeAsync(sessionId: session1.Id, revokedAt: FakeDateProvider.Default.UtcNow);

		Core.Domains.User.UserSession? notRevoked = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "hash-2");
		bool? isActive = notRevoked?.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: notRevoked!.RevokedAt).IsNull();
		await Assert.That(value: isActive).IsTrue();
	}

	[Test]
	public async Task RevokeAllExceptAsync_ShouldRevokeOtherActiveSessions_ButNotTheExcludedOne()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession keep = await CreateAndPersistSessionAsync(userId: userId, hash: "keep-hash");
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "other-hash-1");
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "other-hash-2");

		await _userSessionWriteRepository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: keep.Id,
			revokedAt: FakeDateProvider.Default.UtcNow
		);

		Core.Domains.User.UserSession? kept = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "keep-hash");
		Core.Domains.User.UserSession? revoked1 = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "other-hash-1");
		Core.Domains.User.UserSession? revoked2 = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "other-hash-2");

		await Assert.That(value: kept!.RevokedAt).IsNull();
		await Assert.That(value: revoked1!.RevokedAt).IsNotNull();
		await Assert.That(value: revoked2!.RevokedAt).IsNotNull();
	}

	[Test]
	public async Task RevokeAllExceptAsync_ShouldNotAffectOtherUsersSessions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid otherUserId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession keep = await CreateAndPersistSessionAsync(userId: userId, hash: "mine-keep");
		_ = await CreateAndPersistSessionAsync(userId: otherUserId, hash: "not-mine");

		await _userSessionWriteRepository.RevokeAllExceptAsync(
			userId: userId,
			exceptSessionId: keep.Id,
			revokedAt: FakeDateProvider.Default.UtcNow
		);

		Core.Domains.User.UserSession? untouched = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "not-mine");

		await Assert.That(value: untouched!.RevokedAt).IsNull();
	}

	[Test]
	public async Task RevokeAllAsync_ShouldRevokeEveryActiveSessionIncludingTheCallersOwn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "all-hash-1");
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "all-hash-2");

		await _userSessionWriteRepository.RevokeAllAsync(userId: userId, revokedAt: FakeDateProvider.Default.UtcNow);

		Core.Domains.User.UserSession? s1 = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "all-hash-1");
		Core.Domains.User.UserSession? s2 = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "all-hash-2");

		await Assert.That(value: s1!.RevokedAt).IsNotNull();
		await Assert.That(value: s2!.RevokedAt).IsNotNull();
	}

	[Test]
	public async Task RevokeAllAsync_ShouldNotAffectOtherUsersSessions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid otherUserId = await _userBuilder.CreateAsync();
		_ = await CreateAndPersistSessionAsync(userId: userId, hash: "mine-all");
		_ = await CreateAndPersistSessionAsync(userId: otherUserId, hash: "not-mine-all");

		await _userSessionWriteRepository.RevokeAllAsync(userId: userId, revokedAt: FakeDateProvider.Default.UtcNow);

		Core.Domains.User.UserSession? untouched = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "not-mine-all");

		await Assert.That(value: untouched!.RevokedAt).IsNull();
	}
}
