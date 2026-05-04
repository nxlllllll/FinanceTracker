using FinanceTracker.Application.UseCases.Accounts.Commands.CreateAccount;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
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
		_handler = new CreateAccountHandler(accountRepository: _accountRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnAccountId()
	{
		CreateAccountCommand command = CreateAccountCommandFactory.Create();

		Result<Guid, DomainException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotEqualTo(notExpected: Guid.Empty);
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

		Result<Guid, DomainException> result = await _handler.Handle(command: command, ct: CancellationToken.None);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}
}