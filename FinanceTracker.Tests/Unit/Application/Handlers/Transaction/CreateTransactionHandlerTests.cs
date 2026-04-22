using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transaction;

public sealed class CreateTransactionHandlerTests
{
    private IAccountRepository _accountRepository = null!;
    private ITransactionWriteRepository _transactionWriteRepository = null!;
    private CreateTransactionHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
        _handler = new CreateTransactionHandler(
            accountRepository: _accountRepository,
            transactionWriteRepository: _transactionWriteRepository
        );
    }

    private static FinanceTracker.Core.Domains.Account.Account CreateAccount()
    {
        FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
            userId: Guid.NewGuid(),
            name: "Карта Сбер",
            accountType: "checking",
            currency: "RUB",
            balance: 10000
        );
        account.ClearEvents();
        return account;
    }

    private static CreateTransactionCommand CreateCommand(
        Guid? accountId = null,
        DirectionType direction = DirectionType.Debit)
    {
        return new CreateTransactionCommand(
            AccountId: accountId ?? Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: direction,
            ExchangeRate: 1m,
            Description: "Обед",
            OccurredAt: DateTime.UtcNow
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldReturnTransactionId()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        Guid result = await _handler.Handle(
            command: CreateCommand(),
            ct: CancellationToken.None
        );

        await Assert.That(value: result).IsNotDefault();
    }

    [Test]
    public async Task Handle_WithDebitDirection_ShouldSaveAccountWithDebitEvent()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: CreateCommand(direction: DirectionType.Debit),
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance == 9000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithCreditDirection_ShouldSaveAccountWithCreditEvent()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await _handler.Handle(
            command: CreateCommand(direction: DirectionType.Credit),
            ct: CancellationToken.None
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Balance == 11000m),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithValidCommand_ShouldCreateTransaction()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        CreateTransactionCommand command = CreateCommand();
        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            transactionId: Arg.Any<Guid>(),
            accountId: command.AccountId,
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: command.Amount,
            direction: command.Direction,
            exchangeRate: command.ExchangeRate,
            description: command.Description,
            occurredAt: command.OccurredAt,
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

        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateCommand(), ct: CancellationToken.None)
        ).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountIsArchived_ShouldThrowArchivingException()
    {
        FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
        account.Archive();
        account.ClearEvents();

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        await Assert.That(action: async () =>
            await _handler.Handle(command: CreateCommand(), ct: CancellationToken.None)
        ).Throws<ArchivingException>();
    }
}