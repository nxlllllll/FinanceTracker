using System.IdentityModel.Tokens.Jwt;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Token;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class JwtTokenServiceTests
{
	private IDateProvider _dateProvider = null!;
	private JwtTokenService _tokenService = null!;

	private static readonly JwtOptions TestOptions = new JwtOptions
	{
		Secret = "super-secret-key-at-least-32-characters-long!!",
		Issuer = "FinanceTracker",
		Audience = "FinanceTracker",
		AccessTokenTtlMinutes = 15,
		RefreshTokenTtlDays = 7
	};

	private static readonly User TestUser = User.Reconstitute(
		id: Guid.CreateVersion7(),
		email: Email.Create(value: "test@test.com").Value!,
		passwordHash: "hash",
		baseCurrencyCode: Currency.Create(value: "RUB").Value,
		timeZone: TimeZoneId.Utc,
		rowVersion: 0,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly Guid TestSessionId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_dateProvider = Substitute.For<IDateProvider>();
		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);

		_tokenService = new JwtTokenService(
			options: Options.Create(options: TestOptions),
			dateProvider: _dateProvider
		);
	}

	[Test]
	public async Task GenerateAccessToken_ShouldReturnNonEmptyToken()
	{
		AccessTokenResult result = _tokenService.GenerateAccessToken(user: TestUser, sessionId: TestSessionId);

		await Assert.That(value: result.Token).IsNotEmpty();
	}

	[Test]
	public async Task GenerateAccessToken_ShouldContainSubjectClaim()
	{
		AccessTokenResult result = _tokenService.GenerateAccessToken(user: TestUser, sessionId: TestSessionId);

		JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token: result.Token);
		string? sub = decoded.Claims.FirstOrDefault(predicate: c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

		await Assert.That(value: sub).IsEqualTo(expected: TestUser.Id.ToString());
	}

	[Test]
	public async Task GenerateAccessToken_ShouldContainSessionIdClaim()
	{
		AccessTokenResult result = _tokenService.GenerateAccessToken(user: TestUser, sessionId: TestSessionId);

		JwtSecurityToken decoded = new JwtSecurityTokenHandler().ReadJwtToken(token: result.Token);
		string? sid = decoded.Claims.FirstOrDefault(predicate: c => c.Type == JwtRegisteredClaimNames.Sid)?.Value;

		await Assert.That(value: sid).IsEqualTo(expected: TestSessionId.ToString());
	}

	[Test]
	public async Task GenerateAccessToken_ShouldSetCorrectExpiry()
	{
		AccessTokenResult result = _tokenService.GenerateAccessToken(user: TestUser, sessionId: TestSessionId);

		DateTimeOffset expected = FakeDateProvider.Default.UtcNow.AddMinutes(minutes: TestOptions.AccessTokenTtlMinutes);
		await Assert.That(value: result.ExpiresAt).IsEqualTo(expected: expected);
	}

	[Test]
	public async Task GenerateRefreshToken_ShouldReturnNonEmptyBase64String()
	{
		string token = _tokenService.GenerateRefreshToken();

		await Assert.That(value: token).IsNotEmpty();
		byte[] bytes = Convert.FromBase64String(s: token);
		await Assert.That(value: bytes.Length).IsEqualTo(expected: 32);
	}

	[Test]
	public async Task GenerateRefreshToken_ShouldReturnUniqueTokensEachTime()
	{
		string token1 = _tokenService.GenerateRefreshToken();
		string token2 = _tokenService.GenerateRefreshToken();

		await Assert.That(value: token1).IsNotEqualTo(notExpected: token2);
	}

	[Test]
	public async Task HashRefreshToken_ShouldReturnConsistentHash()
	{
		string hash1 = _tokenService.HashRefreshToken(refreshToken: "some-token");
		string hash2 = _tokenService.HashRefreshToken(refreshToken: "some-token");

		await Assert.That(value: hash1).IsEqualTo(expected: hash2);
	}

	[Test]
	public async Task HashRefreshToken_ShouldReturnDifferentHashesForDifferentTokens()
	{
		string hash1 = _tokenService.HashRefreshToken(refreshToken: "token-one");
		string hash2 = _tokenService.HashRefreshToken(refreshToken: "token-two");

		await Assert.That(value: hash1).IsNotEqualTo(notExpected: hash2);
	}

	[Test]
	public async Task HashRefreshToken_ShouldReturn64CharacterHexString()
	{
		string hash = _tokenService.HashRefreshToken(refreshToken: "any-token");

		await Assert.That(value: hash.Length).IsEqualTo(expected: 64);
		await Assert.That(value: hash).Matches(pattern: "^[0-9a-f]+$");
	}

	[Test]
	public async Task GetRefreshTokenExpiry_ShouldBeCurrentTimePlusTtlDays()
	{
		DateTimeOffset expiry = _tokenService.GetRefreshTokenExpiry();

		DateTimeOffset expected = FakeDateProvider.Default.UtcNow.AddDays(days: TestOptions.RefreshTokenTtlDays);
		await Assert.That(value: expiry).IsEqualTo(expected: expected);
	}
}
