using System.Net;
using FinanceTracker.Application.UseCases.User.Commands.LoginUser;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Auth;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class LoginUserHandlerTests
{
	private IUserAuthRepository _userAuthRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private ISessionIssuer _sessionIssuer = null!;
	private LoginUserHandler _userHandler = null!;
	private IUnitOfWork _unitOfWork = null!;

	private static readonly Email TestEmail = Email.Create(value: "test@test.com").Value!;
	private const string RawPassword = "password123";
	private const string PasswordHash = "argon2hash";
	private readonly IPAddress _testIp = IPAddress.Parse(ipString: "203.0.113.10");

	private static readonly FinanceTracker.Core.Domains.User.User TestUser = FinanceTracker.Core.Domains.User.User.Reconstitute(
		id: Guid.CreateVersion7(),
		email: TestEmail,
		passwordHash: PasswordHash,
		baseCurrencyCode: FinanceTracker.Core.ValueObjects.Currency.Create(value: "RUB").Value,
		rowVersion: 0,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly SessionToken TestSessionToken = new SessionToken(
		AccessToken: "access.token",
		RefreshToken: "refresh-token",
		AccessTokenExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 15),
		SessionId: Guid.CreateVersion7()
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_sessionIssuer = Substitute.For<ISessionIssuer>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<SessionToken>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<SessionToken>>>()?.Invoke());

		_userHandler = new LoginUserHandler(
			userAuthRepository: _userAuthRepository,
			passwordHasher: _passwordHasher,
			sessionIssuer: _sessionIssuer,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnInvalidCredentials()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, AppException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldStillCallPasswordHasherForTimingSafety()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Verify(
			password: RawPassword,
			storedHash: Arg.Is<string?>(predicate: storedHash => storedHash == null)
		);
	}

	[Test]
	public async Task Handle_WhenPasswordInvalid_ShouldReturnInvalidCredentials()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: Arg.Any<string>(),
			storedHash: Arg.Any<string?>()
		).Returns(returnThis: false);

		Result<SessionToken, AppException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: "wrongpassword", IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task Handle_WhenValidCredentials_ShouldCallSessionIssuer()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: RawPassword,
			storedHash: PasswordHash
		).Returns(returnThis: true);
		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestSessionToken);

		await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await _sessionIssuer.Received(requiredNumberOfCalls: 1).IssueAsync(
			user: TestUser,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenValidCredentials_ShouldReturnTokenResponse()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: RawPassword,
			storedHash: PasswordHash
		).Returns(returnThis: true);
		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestSessionToken);

		Result<SessionToken, AppException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.AccessToken).IsEqualTo(expected: TestSessionToken.AccessToken);
	}

	[Test]
	public async Task Handle_WhenValidCredentials_ShouldNotExposeWhichFieldFailed()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, AppException> resultNoUser = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword, IpAddress: _testIp),
			ct: CancellationToken.None
		);

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: Arg.Any<string>(),
			storedHash: Arg.Any<string?>()
		).Returns(returnThis: false);

		Result<SessionToken, AppException> resultWrongPassword = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: "wrong", IpAddress: _testIp),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultNoUser.Error!.GetType())
			.IsEqualTo(expected: resultWrongPassword.Error!.GetType());
	}
}
