using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class UnarchiveAccountHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private IAccountWriteRepository _accountWriteRepository = null!;
    private UnarchiveAccountHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _accountWriteRepository = Substitute.For<IAccountWriteRepository>();
        _handler = new UnarchiveAccountHandler(
            accountRepository: _accountRepository,
            accountWriteRepository: _accountWriteRepository
        );
    }

    private static FinanceTracker.Core.Domains.Account.Account CreateArchivedAccount()
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: Guid.NewGuid(),
            name: "Карта Сбер",
            type: AccountType.Checking,
            currency: "RUB",
            balance: 0
        );
        account.Archive();
        account.ClearEvents();
        return account;
    }

    [Test]
    public async Task Handle_WithArchivedAccount_ShouldUnarchive()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateArchivedAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new UnarchiveAccountCommand(AccountId: account.Id),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
            accountId: account.Id,
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
            command: new UnarchiveAccountCommand(AccountId: Guid.NewGuid()), 
            ct: CancellationToken.None 
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountNotArchived_ShouldThrowUnarchivingException()
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: Guid.NewGuid(),
            name: "Карта Сбер",
            type: AccountType.Checking,
            currency: "RUB",
            balance: 0
        );
        account.ClearEvents();

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () => await _handler.Handle(
            command: new UnarchiveAccountCommand(AccountId: account.Id),
            ct: CancellationToken.None
        )).Throws<UnarchivingException>();
    }

    [Test]
    public async Task Handle_WhenAccountNotArchived_ShouldNotCallWriteRepository()
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: Guid.NewGuid(),
            name: "Карта Сбер",
            type: AccountType.Checking,
            currency: "RUB",
            balance: 0
        );
        account.ClearEvents();

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () => await _handler.Handle(
            command: new UnarchiveAccountCommand(AccountId: account.Id),
            ct: CancellationToken.None
        )).Throws<UnarchivingException>();

        await _accountWriteRepository.DidNotReceive().UnarchiveAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}