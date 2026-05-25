using FinanceTracker.Application.UseCases.Users.Commands.RevokeToken;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.UserSession;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Token;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RevokeTokenHandlerTests
{
	private IUserSessionReadRepository _userSessionReadRepository = null!;
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private ITokenService _tokenService = null!;
	private IDateProvider _dateProvider = null!;
	private RevokeTokenHandler _handler = null!;

	private const string RawRefreshToken = "raw-refresh-token";
	private const string TokenHash = "hashed-token";

	private static UserSession ActiveSession() => UserSession.Reconstitute(
		id: Guid.CreateVersion7(),
		userId: Guid.CreateVersion7(),
		refreshTokenHash: TokenHash,
		expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
		createdAt: DateTimeOffset.UtcNow,
		revokedAt: null
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userSessionReadRepository = Substitute.For<IUserSessionReadRepository>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();
		_tokenService = Substitute.For<ITokenService>();
		_dateProvider = Substitute.For<IDateProvider>();

		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);
		_tokenService.HashRefreshToken(refreshToken: Arg.Any<string>()).Returns(returnThis: TokenHash);

		_handler = new RevokeTokenHandler(
			userSessionReadRepository: _userSessionReadRepository,
			userSessionWriteRepository: _userSessionWriteRepository,
			tokenService: _tokenService,
			dateProvider: _dateProvider
		);
	}

	[Test]
	public async Task Handle_WhenSessionNotFound_ShouldReturnSuccessIdempotently()
	{
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserSession?)null);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userSessionWriteRepository.DidNotReceive().RevokeAsync(
			sessionId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenSessionAlreadyRevoked_ShouldReturnSuccessIdempotently()
	{
		UserSession revokedSession = UserSession.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			refreshTokenHash: TokenHash,
			expiresAt: DateTimeOffset.UtcNow.AddHours(hours: 1),
			createdAt: DateTimeOffset.UtcNow,
			revokedAt: DateTimeOffset.UtcNow.AddMinutes(minutes: -5)
		);
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userSessionWriteRepository.DidNotReceive().RevokeAsync(
			sessionId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenActiveSession_ShouldRevokeIt()
	{
		UserSession session = ActiveSession();
		_userSessionReadRepository.GetByRefreshTokenHashAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: session);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAsync(
			sessionId: session.Id,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
