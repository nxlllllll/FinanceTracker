using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.RegisterUser;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Role;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class RegisterUserHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IUserAuthRepository _userAuthRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IRoleRepository _roleRepository = null!;
	private ISender _sender = null!;
	private RegisterUserHandler _handler = null!;

	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_roleRepository = Substitute.For<IRoleRepository>();
		_sender = Substitute.For<ISender>();

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new RegisterUserHandler(
			userWriteRepository: _userWriteRepository,
			passwordHasher: _passwordHasher,
			userAuthRepository: _userAuthRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default,
			roleRepository: _roleRepository,
			sender: _sender,
			logger: Substitute.For<ILogger<RegisterUserHandler>>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCreateUser()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(user: Arg.Is<FinanceTracker.Core.Domains.User.User>(u =>
			u!.Email == "test@test.com" &&
			u.BaseCurrency == "RUB"
		), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishUserRegisteredNotification()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<UserRegisteredNotification>(n =>
			n!.Email.Value == "test@test.com" &&
			n.BaseCurrency.Value == "RUB"
		));
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldHashPassword()
	{
		_userAuthRepository.GetByEmailAsync(
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
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		Result<Guid, AppException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotDefault();
	}

	[Test]
	public async Task Handle_WithDuplicateEmail_ShouldReturnEmailException()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: UserFactory.Create().Value!);

		Result<Guid, AppException> result = await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task Handle_WithDuplicateEmail_ShouldNotPublishNotification()
	{
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: UserFactory.Create().Value!);

		await _handler.Handle(
			command: RegisterUserCommandFactory.Create(),
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}
}
