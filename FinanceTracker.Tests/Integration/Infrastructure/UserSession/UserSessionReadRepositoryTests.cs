using FinanceTracker.Infrastructure.Database.Repositories.UserSession;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.UserSession;

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
	public async Task GetByRefreshTokenHashAsync_WhenExists_ShouldReturnSession()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Core.Domains.User.UserSession created = await CreateAndPersistSessionAsync(userId: userId, hash: "unique-hash");

		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "unique-hash");

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: created.Id);
		await Assert.That(value: result.RefreshTokenHash).IsEqualTo(expected: "unique-hash");
	}

	[Test]
	public async Task GetByRefreshTokenHashAsync_WhenNotExists_ShouldReturnNull()
	{
		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "nonexistent-hash");

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByRefreshTokenHashAsync_ShouldReturnCorrectRevokedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		DateTime revokedAt = FakeDateProvider.Default.UtcNow.AddMinutes(value: -10);

		await CreateAndPersistSessionAsync(userId: userId, hash: "revoked-hash", revokedAt: revokedAt);

		Core.Domains.User.UserSession? result = await _userSessionReadRepository.GetByRefreshTokenHashAsync(tokenHash: "revoked-hash");

		await Assert.That(value: result!.RevokedAt).IsNotNull();
		await Assert.That(value: result.IsActive).IsFalse();
	}
}