using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class RenameAccountHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private RenameAccountHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _handler = new RenameAccountHandler(accountRepository: _accountRepository);
    }

    [Test]
    public async Task Handle_WithNewName_ShouldSaveAccount()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: "Карта Тинькофф"),
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithSameName_ShouldSaveAccount()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: "Карта Сбер"),
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
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

        await Assert.That(async () => await _handler.Handle(
            command: new RenameAccountCommand(UserId: Guid.NewGuid(), AccountId: Guid.NewGuid(), NewName: "Карта Тинькофф"),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(async () => await _handler.Handle(
            command: new RenameAccountCommand(UserId: Guid.NewGuid(), AccountId: account.Id, NewName: "Карта Тинькофф"),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }
}