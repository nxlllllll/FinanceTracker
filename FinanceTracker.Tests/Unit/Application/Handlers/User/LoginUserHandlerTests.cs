using FinanceTracker.Application.UseCases.User.Commands.LoginUser;
using FinanceTracker.Core.Exceptions.DomainExceptions;
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
	private IUserReadRepository _userReadRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private ISessionIssuer _sessionIssuer = null!;
	private LoginUserHandler _userHandler = null!;

	private static readonly Email TestEmail = Email.Create(value: "test@test.com").Value!;
	private const string RawPassword = "password123";
	private const string PasswordHash = "argon2hash";

	private static readonly FinanceTracker.Core.Domains.User.User TestUser = FinanceTracker.Core.Domains.User.User.Reconstitute(
		id: Guid.CreateVersion7(),
		email: TestEmail,
		passwordHash: PasswordHash,
		baseCurrencyCode: Currency.Create(value: "RUB").Value,
		createdAt: FakeDateProvider.Default.UtcNow
	);

	private static readonly SessionToken TestSessionToken = new SessionToken(
		AccessToken: "access.token",
		RefreshToken: "refresh-token",
		AccessTokenExpiresAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 15)
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_sessionIssuer = Substitute.For<ISessionIssuer>();

		_userHandler = new LoginUserHandler(
			userReadRepository: _userReadRepository,
			passwordHasher: _passwordHasher,
			sessionIssuer: _sessionIssuer
		);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldReturnInvalidCredentials()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, DomainException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task Handle_WhenPasswordInvalid_ShouldReturnInvalidCredentials()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: Arg.Any<string>(),
			hash: Arg.Any<string>()
		).Returns(returnThis: false);

		Result<SessionToken, DomainException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: "wrongpassword"),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task Handle_WhenValidCredentials_ShouldCallSessionIssuer()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: RawPassword,
			hash: PasswordHash
		).Returns(returnThis: true);
		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestSessionToken);

		await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword),
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
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: RawPassword,
			hash: PasswordHash
		).Returns(returnThis: true);
		_sessionIssuer.IssueAsync(
			user: Arg.Any<FinanceTracker.Core.Domains.User.User>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestSessionToken);

		Result<SessionToken, DomainException> result = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.AccessToken).IsEqualTo(expected: TestSessionToken.AccessToken);
		await Assert.That(value: result.Value.RefreshToken).IsEqualTo(expected: TestSessionToken.RefreshToken);
	}

	[Test]
	public async Task Handle_WhenValidCredentials_ShouldNotExposeWhichFieldFailed()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (FinanceTracker.Core.Domains.User.User?)null);

		Result<SessionToken, DomainException> resultNoUser = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: RawPassword),
			ct: CancellationToken.None
		);

		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: TestUser);
		_passwordHasher.Verify(
			password: Arg.Any<string>(),
			hash: Arg.Any<string>()
		).Returns(returnThis: false);

		Result<SessionToken, DomainException> resultWrongPassword = await _userHandler.Handle(
			userCommand: new LoginUserCommand(Email: TestEmail, Password: "wrong"),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultNoUser.Error!.GetType()).IsEqualTo(expected: resultWrongPassword.Error!.GetType());
	}
}