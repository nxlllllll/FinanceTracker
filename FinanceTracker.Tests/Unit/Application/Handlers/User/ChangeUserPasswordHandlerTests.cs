using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserPasswordHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private IPublisher _publisher = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserPasswordHandler _handler = null!;

	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_publisher = Substitute.For<IPublisher>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserPasswordHandler(
			userWriteRepository: _userWriteRepository,
			passwordHasher: _passwordHasher,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldHashPassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPassword: "newPassword"),
			user: user,
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Hash(password: "newPassword");
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangePassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPassword: "newPassword"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangePasswordAsync(
			userId: user.Id,
			newPasswordHash: HashedPassword,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserPasswordChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPassword: "newPassword"),
			user: user,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<UserPasswordChangedNotification>(n => n.UserId == user.Id),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithEmptyPassword_ShouldReturnPasswordException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: String.Empty);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPassword: ""),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
	}

	[Test]
	public async Task HandleAsync_WithEmptyPassword_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: String.Empty);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPassword: ""),
			user: user,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}