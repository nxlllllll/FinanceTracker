using FinanceTracker.Application.UseCases.Users.Commands.RefreshToken;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Repositories.UserSession;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RefreshTokenHandlerTests
{
	private IUserReadRepository _userReadRepository = null!;
	private IUserSessionReadRepository _userSessionReadRepository = null!;
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private ITokenService _tokenService = null!;
	private ISessionIssuer _sessionIssuer = null!;
	private IDateProvider _dateProvider = null!;
	private RefreshTokenHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();
	private const string RawRefreshToken = "raw-refresh-token";
	private const string TokenHash = "hashed-token";

	private static readonly FinanceTracker.Core.Domains.User.User TestUser = FinanceTracker.Core.Domains.User.User.Reconstitute(
		id: UserId,
		email: Email.Create(value: "test@test.com").Value!,
		passwordHash: "hash",
		baseCurrencyCode: Currency.Create(value: "RUB").Value,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly TokenResponse NewTokenResponse = new TokenResponse(
		AccessToken: "new.access.token",
		RefreshToken: "new-refresh-token",
		AccessTokenExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(value: 15)
	);

	private static UserSession ActiveSession()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: UserId,
			refreshTokenHash: TokenHash,
			expiresAt: DateTime.UtcNow.AddHours(value: 1),
			createdAt: DateTime.UtcNow,
			revokedAt: null
		);
	}

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_userSessionReadRepository = Substitute.For<IUserSessionReadRepository>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();
		_tokenService = Substitute.For<ITokenService>();
		_sessionIssuer = Substitute.For<ISessionIssuer>();
		_dateProvider = Substitute.For<IDateProvider>();

		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);
		_tokenService.HashRefreshToken(refreshToken: Arg.Any<string>()).Returns(returnThis: TokenHash);
		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: NewTokenResponse);

		_handler = new RefreshTokenHandler(
			userReadRepository: _userReadRepository,
			userSessionReadRepository: _userSessionReadRepository,
			userSessionWriteRepository: _userSessionWriteRepository,
			tokenService: _tokenService,
			sessionIssuer: _sessionIssuer,
			dateProvider: _dateProvider
		);
	}

	[Test]
	public async Task Handle_WhenSessionNotFound_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserSession?)null);

		Result<TokenResponse, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenSessionIsRevoked_ShouldReturnInvalidToken()
	{
		UserSession revokedSession = UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: UserId,
			refreshTokenHash: TokenHash,
			expiresAt: DateTime.UtcNow.AddHours(value: 1),
			createdAt: DateTime.UtcNow,
			revokedAt: DateTime.UtcNow.AddMinutes(value: -5)
		);
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		Result<TokenResponse, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenSessionIsExpired_ShouldReturnInvalidToken()
	{
		UserSession expiredSession = UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: UserId,
			refreshTokenHash: TokenHash,
			expiresAt: DateTime.UtcNow.AddHours(value: -1),
			createdAt: DateTime.UtcNow.AddHours(value: -2),
			revokedAt: null
		);
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: expiredSession);

		Result<TokenResponse, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<TokenResponse, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldRevokeOldSession()
	{
		UserSession session = ActiveSession();
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: session);
		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAsync(
			sessionId: session.Id,
			revokedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldIssueNewSession()
	{
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await _sessionIssuer.Received(requiredNumberOfCalls: 1).IssueAsync(
			user: TestUser,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldReturnNewTokenResponse()
	{
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userReadRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		Result<TokenResponse, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.AccessToken).IsEqualTo(expected: NewTokenResponse.AccessToken);
		await Assert.That(value: result.Value.RefreshToken).IsEqualTo(expected: NewTokenResponse.RefreshToken);
	}
}