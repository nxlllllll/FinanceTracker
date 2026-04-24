using FinanceTracker.Application.Accounts.Commands.CreateAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class CreateAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private CreateAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new CreateAccountHandler(accountRepository: _accountRepository);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnAccountId()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldSaveAccount()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(account =>
				account.Name == command.Name &&
				account.UserId == command.UserId &&
				account.Type == command.Type &&
				account.Currency == command.Currency
			), ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Handle_WithEmptyName_ShouldThrowArgumentException()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create(name: String.Empty);

		await Assert.That(
			func: async () => await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<EmptyNameException>();
	}
}