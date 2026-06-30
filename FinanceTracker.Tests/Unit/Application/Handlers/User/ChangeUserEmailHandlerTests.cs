using FinanceTracker.Application.UseCases.User.Commands.ChangeUserEmail;
using FinanceTracker.Application.UseCases.User.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserEmailHandlerTests
{
	private IUserAuthRepository _userAuthRepository = null!;
	private IUserWriteRepository _userWriteRepository = null!;
	private IPublisher _publisher = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ChangeUserEmailHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userAuthRepository = Substitute.For<IUserAuthRepository>();
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ChangeUserEmailHandler(
			userAuthRepository: _userAuthRepository,
			userWriteRepository: _userWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeUserEmailHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeEmail()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			accounts: user,
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
	public async Task HandleAsync_WithValidCommand_ShouldPublishUserEmailChangedNotification()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		_userAuthRepository.GetByEmailAsync(
			email: Arg.Any<string>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			accounts: user,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(notification: Arg.Is<UserEmailChangedNotification>(n =>
			n.UserId == user.Id &&
			n.NewEmail.Value == "new@test.com"
		), cancellationToken: Arg.Any<CancellationToken>());
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

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			accounts: user,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<EmailException>();
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
			command: new ChangeUserEmailCommand(UserId: user.Id, NewEmail: "new@test.com"),
			accounts: user,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<INotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}