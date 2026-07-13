using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserPassword;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
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
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserPasswordHandler _handler = null!;

	private const string CurrentPassword = "currentPassword";
	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: true);
		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new ChangeUserPasswordHandler(
			userWriteRepository: _userWriteRepository,
			userSessionWriteRepository: _userSessionWriteRepository,
			passwordHasher: _passwordHasher,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldVerifyCurrentPassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create(passwordHash: "storedHash").Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Verify(password: CurrentPassword, storedHash: "storedHash");
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldHashNewPassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: "newPassword"
			),
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
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangePasswordAsync(
			userId: user.Id,
			newPasswordHash: HashedPassword,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldRevokeAllOtherSessions()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		Guid currentSessionId = Guid.CreateVersion7();

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: currentSessionId,
				CurrentPassword: CurrentPassword,
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.Received(requiredNumberOfCalls: 1).RevokeAllExceptAsync(
			userId: user.Id,
			exceptSessionId: currentSessionId,
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserPasswordChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Is<UserPasswordChangedNotification>(n => n!.UserId == user.Id)
		);
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldReturnInvalidCredentialsException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: "wrongPassword",
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotHashNewPassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: "wrongPassword",
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _passwordHasher.DidNotReceive().Hash(password: Arg.Any<string>());
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotChangePassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: "wrongPassword",
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.DidNotReceive().ChangePasswordAsync(
			userId: Arg.Any<Guid>(),
			newPasswordHash: Arg.Any<string>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotRevokeSessions()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: "wrongPassword",
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.DidNotReceive().RevokeAllExceptAsync(
			userId: Arg.Any<Guid>(),
			exceptSessionId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: "wrongPassword",
				NewPassword: "newPassword"
			),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}

	[Test]
	public async Task HandleAsync_WithEmptyNewPassword_ShouldReturnPasswordException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: String.Empty);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: String.Empty
			),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<PasswordException>();
	}

	[Test]
	public async Task HandleAsync_WithEmptyNewPassword_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: String.Empty);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: String.Empty
			),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}

	[Test]
	public async Task HandleAsync_WithEmptyNewPassword_ShouldNotRevokeSessions()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: String.Empty);

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(
				UserId: user.Id,
				CurrentSessionId: Guid.CreateVersion7(),
				CurrentPassword: CurrentPassword,
				NewPassword: String.Empty
			),
			user: user,
			ct: CancellationToken.None
		);

		await _userSessionWriteRepository.DidNotReceive().RevokeAllExceptAsync(
			userId: Arg.Any<Guid>(),
			exceptSessionId: Arg.Any<Guid>(),
			revokedAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
