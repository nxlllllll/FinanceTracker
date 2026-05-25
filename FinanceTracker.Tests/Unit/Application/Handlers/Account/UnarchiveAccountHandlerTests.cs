using FinanceTracker.Application.UseCases.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class UnarchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private UnarchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new UnarchiveAccountHandler(accountRepository: _accountRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task HandleAsync_WithArchivedAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		await _handler.HandleAsync(
			command: new UnarchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyActive_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new UnarchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnarchivingException>();
	}
}
