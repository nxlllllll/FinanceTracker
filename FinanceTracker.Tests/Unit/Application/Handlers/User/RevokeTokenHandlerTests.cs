using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.RevokeToken;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
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
	private IUnitOfWork _unitOfWork = null!;
	private ITokenService _tokenService = null!;
	private IDateProvider _dateProvider = null!;
	private RevokeTokenHandler _handler = null!;

	private const string RawRefreshToken = "raw-refresh-token";
	private const string TokenHash = "hashed-token";
	private readonly IPAddress _testIp = IPAddress.Parse(ipString: "203.0.113.10");

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
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_tokenService = Substitute.For<ITokenService>();
		_dateProvider = Substitute.For<IDateProvider>();

		_dateProvider.UtcNow.Returns(returnThis: FakeDateProvider.Default.UtcNow);
		_tokenService.HashRefreshToken(refreshToken: Arg.Any<string>()).Returns(returnThis: TokenHash);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		
		_handler = new RevokeTokenHandler(
			userSessionReadRepository: _userSessionReadRepository,
			userSessionWriteRepository: _userSessionWriteRepository,
			unitOfWork: _unitOfWork,
			tokenService: _tokenService,
			dateProvider: _dateProvider
		);
	}

	[Test]
	public async Task Handle_WhenSessionNotFound_ShouldReturnSuccessIdempotently()
	{
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (UserSession?)null);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
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
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: revokedSession);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
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
		_userSessionReadRepository.GetByRefreshTokenHashForUpdateAsync(
			tokenHash: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: session);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = await _handler.Handle(
			command: new RevokeTokenCommand(RefreshToken: RawRefreshToken, IpAddress: _testIp),
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