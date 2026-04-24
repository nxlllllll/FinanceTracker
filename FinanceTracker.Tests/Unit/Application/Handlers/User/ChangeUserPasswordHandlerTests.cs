using FinanceTracker.Application.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserPasswordHandlerTests
{
	private IUserRepository _userRepository = null!;
	private ChangeUserPasswordHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userRepository = Substitute.For<IUserRepository>();
		_handler = new ChangeUserPasswordHandler(userRepository: _userRepository);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldChangePassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create();
		_userRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(UserId: user.Id, NewPasswordHash: "newHash");

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userRepository.Received(requiredNumberOfCalls: 1).ChangePasswordAsync(
			userId: user.Id,
			newPasswordHash: "newHash",
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenUserNotFound_ShouldThrowNotFoundException()
	{
		_userRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.User.User?>(result: null));

		ChangeUserPasswordCommand command = new ChangeUserPasswordCommand(UserId: Guid.NewGuid(), NewPasswordHash: "newHash");

		await Assert.That(action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)).Throws<NotFoundException>();
	}
}