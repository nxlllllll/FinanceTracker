using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Auth;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class SessionIssuerTests
{
	private ITokenService _tokenService = null!;
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private SessionIssuer _sessionIssuer = null!;

	private static readonly User TestUser = User.Reconstitute(
		id: Guid.CreateVersion7(),
		email: Email.Create(value: "test@test.com").Value!,
		passwordHash: "hash",
		baseCurrencyCode: Currency.Create(value: "RUB").Value,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly AccessTokenResult TestAccessToken = new AccessTokenResult(
		Token: "access.token.value",
		ExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 15)
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_tokenService = Substitute.For<ITokenService>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();

		_tokenService.GenerateAccessToken(user: Arg.Any<User>()).Returns(returnThis: TestAccessToken);
		_tokenService.GenerateRefreshToken().Returns(returnThis: "raw-refresh-token");
		_tokenService.HashRefreshToken(refreshToken: Arg.Any<string>()).Returns(returnThis: "hashed-refresh-token");
		_tokenService.GetRefreshTokenExpiry().Returns(returnThis: FakeDateProvider.Default.UtcNow.AddDays(days: 7));

		_sessionIssuer = new SessionIssuer(
			tokenService: _tokenService,
			userSessionWriteRepository: _userSessionWriteRepository,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task IssueAsync_ShouldCallGenerateAccessToken()
	{
		await _sessionIssuer.IssueAsync(user: TestUser);

		_tokenService.Received(requiredNumberOfCalls: 1).GenerateAccessToken(user: TestUser);
	}

	[Test]
	public async Task IssueAsync_ShouldCallGenerateRefreshToken()
	{
		await _sessionIssuer.IssueAsync(user: TestUser);

		_tokenService.Received(requiredNumberOfCalls: 1).GenerateRefreshToken();
	}

	[Test]
	public async Task IssueAsync_ShouldPersistSession()
	{
		await _sessionIssuer.IssueAsync(user: TestUser);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			session: Arg.Any<UserSession>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task IssueAsync_ShouldReturnCorrectAccessToken()
	{
		TokenResponse response = await _sessionIssuer.IssueAsync(user: TestUser);

		await Assert.That(value: response.AccessToken).IsEqualTo(expected: TestAccessToken.Token);
		await Assert.That(value: response.AccessTokenExpiresAt).IsEqualTo(expected: TestAccessToken.ExpiresAt);
	}

	[Test]
	public async Task IssueAsync_ShouldReturnRawRefreshToken()
	{
		TokenResponse response = await _sessionIssuer.IssueAsync(user: TestUser);

		await Assert.That(value: response.RefreshToken).IsEqualTo(expected: "raw-refresh-token");
	}

	[Test]
	public async Task IssueAsync_ShouldStoreHashedRefreshToken()
	{
		await _sessionIssuer.IssueAsync(user: TestUser);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			session: Arg.Is<UserSession>(s => s.RefreshTokenHash == "hashed-refresh-token"),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
