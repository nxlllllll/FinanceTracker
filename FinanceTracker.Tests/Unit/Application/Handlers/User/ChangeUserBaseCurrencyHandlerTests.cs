using FinanceTracker.Application.UseCases.Users.Commands.ChangeUserBaseCurrency;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.User;

public sealed class ChangeUserBaseCurrencyHandlerTests
{
	private IUserWriteRepository _userWriteRepository = null!;
	private ChangeUserBaseCurrencyHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_userWriteRepository = Substitute.For<IUserWriteRepository>();
		_handler = new ChangeUserBaseCurrencyHandler(userWriteRepository: _userWriteRepository);
	}

	[Test]
	public async Task HandleAsync_WithValidCommand_ShouldChangeBaseCurrency()
	{
		FinanceTracker.Core.Domains.User.User user = UserFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeUserBaseCurrencyCommand(UserId: user.Id, NewBaseCurrency: "USD"),
			user: user,
			ct: CancellationToken.None
		);

		await _userWriteRepository.Received(requiredNumberOfCalls: 1).ChangeBaseCurrencyAsync(
			userId: Arg.Is(value: user.Id),
			newBaseCurrencyCode: Arg.Is<Currency>(value: Currency.Create(value: "USD").Value),
			ct: Arg.Any<CancellationToken>()
		);
	}
}