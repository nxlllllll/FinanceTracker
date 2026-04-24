using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class RenameAccountHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private IAccountWriteRepository _accountWriteRepository = null!;
    private RenameAccountHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _accountWriteRepository = Substitute.For<IAccountWriteRepository>();
        _handler = new RenameAccountHandler(
            accountRepository: _accountRepository,
            accountWriteRepository: _accountWriteRepository
        );
    }

    private static FinanceTracker.Core.Domains.Account.Account CreateAccount(string name = "Карта Сбер")
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: Guid.NewGuid(),
            name: name,
            type: AccountType.Checking,
            currency: "RUB",
            balance: 0
        );
        account.ClearEvents();
        return account;
    }

    [Test]
    public async Task Handle_WithNewName_ShouldRename()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new RenameAccountCommand(AccountId: account.Id, NewName: "Карта Тинькофф"),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
            accountId: account.Id,
            newName: "Карта Тинькофф",
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithSameName_ShouldNotCallWriteRepository()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount(name: "Карта Сбер");
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new RenameAccountCommand(AccountId: account.Id, NewName: "Карта Сбер"),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.DidNotReceive().RenameAsync(
            accountId: Arg.Any<Guid>(),
            newName: Arg.Any<string>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountNotFound_ShouldThrowNotFoundException()
    {
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

        await Assert.That(action: async () => await _handler.Handle(
            command: new RenameAccountCommand(AccountId: Guid.NewGuid(), NewName: "Карта Тинькофф"),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }
}