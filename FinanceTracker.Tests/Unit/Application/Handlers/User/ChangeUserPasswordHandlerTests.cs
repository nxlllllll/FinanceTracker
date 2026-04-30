using FinanceTracker.Application.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserPasswordHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private ChangeUserPasswordHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_handler = new ChangeUserPasswordHandler(userWriteRepository: _userWriteRepository);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangePassword()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create();

		await _handler.HandleAsync(
			command: new ChangeUserPasswordCommand(UserId: user.Id, NewPasswordHash: "newHash"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangePasswordAsync(
			userId: user.Id,
			newPasswordHash: "newHash",
			ct: Arg.Any<CancellationToken>()
		);
	}
}