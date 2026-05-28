using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
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
	private IUserAuthRepository _userAuthRepository = null!;
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

	private static readonly SessionToken NewSessionToken = new SessionToken(
		AccessToken: "new.access.token",
		RefreshToken: "new-refresh-token",
		AccessTokenExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 15)
	);

	private static UserSession ActiveSession()
	{
		return UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: UserId,
			refreshTokenHash: TokenHash,
			expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
			createdAt: DateTimeOffset.UtcNow,
			revokedAt: null
		);
	}

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
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
		).Returns(returnThis: NewSessionToken);

		_handler = new RefreshTokenHandler(
			userAuthRepository: _userAuthRepository,
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

		Result<SessionToken, DomainException> result = await _handler.Handle(
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
			expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
			createdAt: DateTimeOffset.UtcNow,
			revokedAt: DateTimeOffset.UtcNow.AddMinutes(minutes: -5)
		);
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		Result<SessionToken, DomainException> result = await _handler.Handle(
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
			expiresAt: DateTimeOffset.UtcNow.AddHours(hours: -1),
			createdAt: DateTimeOffset.UtcNow.AddHours(hours: -2),
			revokedAt: null
		);
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: expiredSession);

		Result<SessionToken, DomainException> result = await _handler.Handle(
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
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, DomainException> result = await _handler.Handle(
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
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAsync(
			sessionId: session.Id,
			revokedAt: Arg.Any<DateTimeOffset>(),
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
		_userAuthRepository.GetByIdAsync(
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
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		Result<SessionToken, DomainException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.AccessToken).IsEqualTo(expected: NewSessionToken.AccessToken);
		await Assert.That(value: result.Value.RefreshToken).IsEqualTo(expected: NewSessionToken.RefreshToken);
	}
}