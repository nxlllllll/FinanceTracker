using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserEmailHandlerTests
{
	private IUserAuthRepository _userAuthRepository = null!;
	private IUserWriteRepository _userWriteRepository = null!;
	private IUserSessionWriteRepository _userSessionWriteRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserEmailHandler _handler = null!;

	private const string CurrentPassword = "currentPassword";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_userSessionWriteRepository = Substitute.For<IUserSessionWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: true);
		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserEmailHandler(
			userAuthRepository: _userAuthRepository,
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
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _passwordHasher.Received(requiredNumberOfCalls: 1).Verify(password: CurrentPassword, storedHash: "storedHash");
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeEmail()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeEmailAsync(
			userId: user.Id,
			newEmail: Email.Create(value: "new@test.com").Value,
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
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: currentSessionId, CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
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
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserEmailChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<UserEmailChangedNotification>(n =>
			n.UserId == user.Id &&
			n.NewEmail.Value == "new@test.com"
		));
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldReturnInvalidCredentialsException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: "wrongPassword", NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidCredentialsException>();
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotCheckEmailUniqueness()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: "wrongPassword", NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _userAuthRepository.DidNotReceive().GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithWrongCurrentPassword_ShouldNotChangeEmail()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_passwordHasher.Verify(password: Arg.Any<string>(), storedHash: Arg.Any<string>()).Returns(returnThis: false);

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: "wrongPassword", NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.DidNotReceive().ChangeEmailAsync(
			userId: Arg.Any<Guid>(),
			newEmail: Arg.Any<Email>(),
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
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: "wrongPassword", NewEmail: "new@test.com"),
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
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: "wrongPassword", NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}

	[Test]
	public async Task HandleAsync_WithDuplicateEmail_ShouldReturnEmailException()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: anotherUser);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
	}

	[Test]
	public async Task HandleAsync_WithDuplicateEmail_ShouldNotRevokeSessions()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: anotherUser);

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
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
	public async Task HandleAsync_WithDuplicateEmail_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;
		FinanceTracker.Core.Domains.User.User anotherUser = UserFactory.Create(email: "new@test.com").Value!;

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: anotherUser);

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, CurrentSessionId: Guid.CreateVersion7(), CurrentPassword: CurrentPassword, NewEmail: "new@test.com"),
			user: user,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<INotification>());
	}
}
