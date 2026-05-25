using FinanceTracker.Application.UseCases.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ArchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new ArchiveAccountHandler(accountRepository: _accountRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task HandleAsync_WithActiveAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
