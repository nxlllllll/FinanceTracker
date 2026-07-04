using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.RefreshToken;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RefreshTokenHandlerTests
{
	private IUserAuthRepository _userAuthRepository = null!;
	private IUserSessionReadRepository _userSessionReadRepository = null!;
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ITokenService _tokenService = null!;
	private ISessionIssuer _sessionIssuer = null!;
	private IPublisher _publisher = null!;
	private IDateProvider _dateProvider = null!;
	private RefreshTokenHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();
	private const string RawRefreshToken = "raw-refresh-token";
	private const string TokenHash = "hashed-token";
	private readonly IPAddress _testIp = IPAddress.Parse(ipString: "203.0.113.10");

	private static readonly FinanceTracker.Core.Domains.User.User TestUser = FinanceTracker.Core.Domains.User.User.Reconstitute(
		id: UserId,
		email: Email.Create(value: "test@test.com").Value!,
		passwordHash: "hash",
		baseCurrencyCode: Currency.Create(value: "RUB").Value,
		rowVersion: 0,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly SessionToken NewSessionToken = new SessionToken(
		AccessToken: "new.access.token",
		RefreshToken: "new-refresh-token",
		AccessTokenExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 15)
	);

	private static UserSession ActiveSession() => UserSession.Reconstitute(
		id: Guid.CreateVersion7(),
		userId: UserId,
		refreshTokenHash: TokenHash,
		expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
		createdAt: DateTimeOffset.UtcNow,
		revokedAt: null
	);

	private static UserSession RevokedSession() => UserSession.Reconstitute(
		id: Guid.CreateVersion7(),
		userId: UserId,
		refreshTokenHash: TokenHash,
		expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
		createdAt: DateTimeOffset.UtcNow,
		revokedAt: DateTimeOffset.UtcNow.AddMinutes(minutes: -5)
	);

	private static UserSession ExpiredButNotRevokedSession() => UserSession.Reconstitute(
		id: Guid.CreateVersion7(),
		userId: UserId,
		refreshTokenHash: TokenHash,
		expiresAt: DateTimeOffset.UtcNow.AddHours(hours: -1),
		createdAt: DateTimeOffset.UtcNow.AddHours(hours: -2),
		revokedAt: null
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userSessionReadRepository = Substitute.For<IUserSessionReadRepository>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_tokenService = Substitute.For<ITokenService>();
		_sessionIssuer = Substitute.For<ISessionIssuer>();
		_publisher = Substitute.For<IPublisher>();
		_dateProvider = Substitute.For<IDateProvider>();

		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);
		_tokenService.HashRefreshToken(refreshToken: Arg.Any<string>()).Returns(returnThis: TokenHash);

		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: NewSessionToken);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<RefreshTokenHandler.RotateResult>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<RefreshTokenHandler.RotateResult>>>()());

		_handler = new RefreshTokenHandler(
			userAuthRepository: _userAuthRepository,
			userSessionReadRepository: _userSessionReadRepository,
			userSessionWriteRepository: _userSessionWriteRepository,
			tokenService: _tokenService,
			sessionIssuer: _sessionIssuer,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: _dateProvider,
			logger: Substitute.For<ILogger<RefreshTokenHandler>>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionNotFound_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserSession?)null);

		Result<SessionToken, AppException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenSessionNotFound_ShouldNotRevokeAnySessions()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserSession?)null);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.DidNotReceive().RevokeAllAsync(
			userId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionIsRevoked_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: RevokedSession());

		Result<SessionToken, AppException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenSessionIsRevoked_ShouldRevokeAllUserSessions()
	{
		UserSession revokedSession = RevokedSession();
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAllAsync(
			userId: revokedSession.UserId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionIsRevoked_ShouldPublishReuseDetectedNotification()
	{
		UserSession revokedSession = RevokedSession();
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<RefreshTokenReuseDetectedNotification>(n => n.UserId == revokedSession.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionIsExpiredButNotRevoked_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ExpiredButNotRevokedSession());

		Result<SessionToken, AppException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenSessionIsExpiredButNotRevoked_ShouldNotRevokeAllUserSessions()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ExpiredButNotRevokedSession());

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.DidNotReceive().RevokeAllAsync(
			userId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionIsExpiredButNotRevoked_ShouldNotPublishNotification()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ExpiredButNotRevokedSession());

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnInvalidToken()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, AppException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTokenException>();
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldRevokeOldSession()
	{
		UserSession session = ActiveSession();
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: session);
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAsync(
			sessionId: session.Id,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldNotRevokeAllUserSessions()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.DidNotReceive().RevokeAllAsync(
			userId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenValidToken_ShouldIssueNewSession()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
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
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ActiveSession());
		_userAuthRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);

		Result<SessionToken, AppException> result = await _handler.Handle(
			command: new RefreshTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.AccessToken).IsEqualTo(expected: NewSessionToken.AccessToken);
		await Assert.That(value: result.Value.RefreshToken).IsEqualTo(expected: NewSessionToken.RefreshToken);
	}
}
