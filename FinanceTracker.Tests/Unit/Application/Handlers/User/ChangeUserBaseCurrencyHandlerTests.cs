using FinanceTracker.Application.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserBaseCurrencyHandlerTests
{
	private IUserRepository _userRepository;
	private ChangeUserBaseCurrencyHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userRepository = Substitute.For<IUserRepository>();
		_handler = new ChangeUserBaseCurrencyHandler(_userRepository);
	}

	private static FinanceTracker.Core.Domains.User.User CreateUser()
	{
		return FinanceTracker.Core.Domains.User.User.Register(
			email: "test@test.com",
			passwordHash: "hash",
			baseCurrencyCode: "RUB"
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = CreateUser();
		_userRepository.GetByIdAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: user);

		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: "USD");

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _userRepository.Received(requiredNumberOfCalls: 1).ChangeBaseCurrencyAsync(
			userId: user.Id,
			newBaseCurrencyCode: "USD",
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

		ChangeUserBaseCurrencyCommand command = new ChangeUserBaseCurrencyCommand(UserId: Guid.NewGuid(), NewBaseCurrency: "USD");

		await Assert.That(action: async () => await _handler.Handle(command: command, ct: CancellationToken.None)).Throws<NotFoundException>();
	}
}