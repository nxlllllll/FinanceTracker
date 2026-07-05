using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class RenameAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private RenameAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(callInfo => callInfo.Arg<Func<Task>>()());

		_handler = new RenameAccountHandler(
			accountRepository: _accountRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_ShouldSaveAccountWithNewName()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();

		await _handler.HandleAsync(
			command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: Name.Create(value: "Карта Тинькофф").Value),
			user: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Name.Value == "Карта Тинькофф"),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenNameUnchanged_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(name: "Карта Сбер").Value!;
		Name sameName = Name.Create(value: "Карта Сбер").Value!;

		account.ClearEvents();

		await _handler.HandleAsync(
			command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: sameName),
			user: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
