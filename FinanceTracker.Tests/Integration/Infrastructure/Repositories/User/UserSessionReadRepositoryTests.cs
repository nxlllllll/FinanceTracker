using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class UserSessionReadRepositoryTests : DatabaseFixture
{
	private UserSessionReadRepository _userSessionReadRepository = null!;
	private UserSessionWriteRepository _userSessionWriteRepository = null!;
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
			expiresAt: expiresAt ?? FakeDateProvider.Default.UtcNow.AddDays(days: 7),
			createdAt: FakeDateProvider.Default.UtcNow,
			revokedAt: revokedAt,
			supersededBySessionId: null
		);

		await _userSessionWriteRepository.CreateAsync(session: session);
		await Context.SaveChangesAsync();
		return session;
	}

	[Test]
	public async Task GetByRefreshTokenHashAsync_WhenExists_ShouldReturnSession()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession created = await CreateAndPersistSessionAsync(userId: userId, hash: "unique-hash");

		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "unique-hash");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: created.Id);
		await Assert.That(value: result.RefreshTokenHash).IsEqualTo(expected: "unique-hash");
	}

	[Test]
	public async Task GetByRefreshTokenHashAsync_WhenNotExists_ShouldReturnNull()
	{
		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "nonexistent-hash");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByRefreshTokenHashAsync_ShouldReturnCorrectRevokedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		DateTimeOffset revokedAt = FakeDateProvider.Default.UtcNow.AddMinutes(minutes: -10);

		await CreateAndPersistSessionAsync(userId: userId, hash: "revoked-hash", revokedAt: revokedAt);

		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(tokenHash: "revoked-hash");
		bool? isActive = result?.IsActive(now: FakeDateProvider.Default.UtcNow);

		await Assert.That(value: result?.RevokedAt).IsNotNull();
		await Assert.That(value: isActive).IsFalse();
	}
}
