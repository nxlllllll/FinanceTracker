using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private IAccountWriteRepository _accountWriteRepository = null!;
    private ArchiveAccountHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _accountWriteRepository = Substitute.For<IAccountWriteRepository>();
        _handler = new ArchiveAccountHandler(
            accountRepository: _accountRepository,
            accountWriteRepository: _accountWriteRepository
        );
    }

    private static FinanceTracker.Core.Domains.Account.Account CreateAccount(Guid? userId = null, bool archived = false)
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: userId ?? Guid.NewGuid(),
            name: "Карта Сбер",
            type: AccountType.Checking,
            currency: "RUB",
            balance: 0
        );
        account.ClearEvents();

        if (archived)
            account.Archive();

        return account;
    }

    [Test]
    public async Task Handle_WithActiveAccount_ShouldArchive()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
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
            command: new ArchiveAccountCommand(UserId: Guid.NewGuid(), AccountId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () => await _handler.Handle(
            command: new ArchiveAccountCommand(UserId: Guid.NewGuid(), AccountId: account.Id),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountAlreadyArchived_ShouldThrowArchivingException()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount(archived: true);
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () => await _handler.Handle(
            command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
            ct: CancellationToken.None
        )).Throws<ArchivingException>();
    }

    [Test]
    public async Task Handle_WhenAccountAlreadyArchived_ShouldNotCallWriteRepository()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount(archived: true);
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () => await _handler.Handle(
            command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
            ct: CancellationToken.None
        )).Throws<ArchivingException>();

        await _accountWriteRepository.DidNotReceive().ArchiveAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}