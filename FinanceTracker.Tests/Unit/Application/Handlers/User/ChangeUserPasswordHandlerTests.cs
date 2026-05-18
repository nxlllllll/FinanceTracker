using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserPassword;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.Password;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserPasswordHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private IPasswordHasher _passwordHasher = null!;
	private ChangeUserPasswordHandler _handler = null!;

	private const string HashedPassword = "hashed_password_value";

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_passwordHasher = Substitute.For<IPasswordHasher>();

		_passwordHasher.Hash(password: Arg.Any<string>()).Returns(returnThis: HashedPassword);

		_handler = new ChangeUserPasswordHandler(
			userWriteRepository: _userWriteRepository,
			passwordHasher: _passwordHasher
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
}