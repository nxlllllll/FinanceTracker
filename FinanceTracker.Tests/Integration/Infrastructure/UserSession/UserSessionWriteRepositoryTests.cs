using FinanceTracker.Infrastructure.Database.Repositories.UserSession;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.UserSession;

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
		DateTime? expiresAt = null,
		DateTime? revokedAt = null)
	{
		Core.Domains.User.UserSession session = Core.Domains.User.UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: userId,
			refreshTokenHash: hash,
			expiresAt: expiresAt ?? DateTime.UtcNow.AddDays(value: 7),
			createdAt: FakeDateProvider.Default.UtcNow,
			revokedAt: revokedAt
		);

		await _userSessionWriteRepository.CreateAsync(session: session);
		return session;
	}

	[Test]
	public async Task CreateAsync_ShouldPersistSession()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session = Core.Domains.User.UserSession.Create(
			userId: userId,
			refreshTokenHash: "testhash",
			expiresAt: DateTime.UtcNow.AddDays(value: 7),
			createdAt: FakeDateProvider.Default.UtcNow
		);

		await _userSessionWriteRepository.CreateAsync(session: session);

		Core.Domains.User.UserSession? loaded = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "testhash");
		await Assert.That(value: loaded).IsNotNull();
		await Assert.That(value: loaded!.Id).IsEqualTo(expected: session.Id);
		await Assert.That(value: loaded.UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task RevokeAsync_ShouldSetRevokedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session = await CreateAndPersistSessionAsync(userId: userId, hash: "revoke-test-hash");
		DateTime revokedAt = FakeDateProvider.Default.UtcNow;

		await _userSessionWriteRepository.RevokeAsync(sessionId: session.Id, revokedAt: revokedAt);

		Core.Domains.User.UserSession? revoked = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "revoke-test-hash");
		await Assert.That(value: revoked!.RevokedAt).IsNotNull();
		await Assert.That(value: revoked.IsActive).IsFalse();
	}

	[Test]
	public async Task RevokeAsync_ShouldNotAffectOtherSessions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession session1 = await CreateAndPersistSessionAsync(userId: userId, hash: "hash-1");
		Core.Domains.User.UserSession session2 = await CreateAndPersistSessionAsync(userId: userId, hash: "hash-2");

		await _userSessionWriteRepository.RevokeAsync(sessionId: session1.Id, revokedAt: FakeDateProvider.Default.UtcNow);

		Core.Domains.User.UserSession? notRevoked = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "hash-2");
		await Assert.That(value: notRevoked!.RevokedAt).IsNull();
		await Assert.That(value: notRevoked.IsActive).IsTrue();
	}
}