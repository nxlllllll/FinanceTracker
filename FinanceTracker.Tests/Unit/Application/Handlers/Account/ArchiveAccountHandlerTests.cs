using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ArchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new ArchiveAccountHandler(
			accountRepository: _accountRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			accounts: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldReturnArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(archived: true);

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			accounts: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(archived: true);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			accounts: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
