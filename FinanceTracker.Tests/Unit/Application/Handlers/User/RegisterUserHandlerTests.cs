using FinanceTracker.Application.UseCases.Users.Commands.RegisterUser;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RegisterUserHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IUserReadRepository _userReadRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private RegisterUserHandler _handler = null!;

	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userReadRepository = Substitute.For<IUserReadRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);

		_handler = new RegisterUserHandler(
			userWriteRepository: _userWriteRepository,
			passwordHasher: _passwordHasher,
			userReadRepository: _userReadRepository,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<RegisterUserHandler>>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateUser()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			user: Arg.Is<FinanceTracker.Core.Domains.User.User>(u =>
				u.Email == "test@test.com" &&
				u.BaseCurrency == "RUB"
			),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldHashPassword()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(password: "password123"),
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Hash(password: "password123");
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnUserId()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		Result<Guid, DomainException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotDefault();
	}

	[Test]
	public async Task Handle_WithDuplicateEmail_ShouldReturnEmailException()
	{
		_userReadRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: UserFactory.Create().Value!);

		Result<Guid, DomainException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}
}