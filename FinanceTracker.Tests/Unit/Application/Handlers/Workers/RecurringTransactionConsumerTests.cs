using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Workers;

public sealed class RecurringTransactionConsumerTests
{
    private IAccountRepository _accountRepository = null!;
    private ITransactionCreationService _transactionCreationService = null!;
    private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
    private RecurringTransactionConsumer _consumer = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionCreationService = Substitute.For<ITransactionCreationService>();
        _recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();

        _consumer = new RecurringTransactionConsumer(
            accountRepository: _accountRepository,
            transactionCreationService: _transactionCreationService,
            recurringTransactionReadRepository: _recurringTransactionReadRepository,
            logger: Substitute.For<ILogger<RecurringTransactionConsumer>>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenRecurringTransactionNotFound_ShouldSkip()
    {
        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction?)null);

        RecurringTransactionTriggeredMessage message = BuildMessage();

        await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

        await _transactionCreationService.DidNotReceive().CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenAccountNotFound_ShouldThrow()
    {
        FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction =
            RecurringTransactionFactory.Create().Value!;

        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: recurringTransaction);

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

        RecurringTransactionTriggeredMessage message = BuildMessage();

        await Assert.ThrowsAsync<NotFoundException>(
            action: async () => await _consumer.HandleAsync(message: message, ct: CancellationToken.None)
        );
    }

    [Test]
    public async Task HandleAsync_WhenValid_ShouldCreateTransaction()
    {
        FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction =
            RecurringTransactionFactory.Create().Value!;

        FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create().Value!;

        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: recurringTransaction);

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: account);

        _transactionCreationService.CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Result<Guid, DomainException>.Success(value: Guid.CreateVersion7()));

        RecurringTransactionTriggeredMessage message = BuildMessage();

        await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

        await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    private static RecurringTransactionTriggeredMessage BuildMessage()
    {
        return new RecurringTransactionTriggeredMessage(
            MessageId: Guid.CreateVersion7(),
            RecurringTransactionId: Guid.CreateVersion7(),
            AccountId: Guid.CreateVersion7(),
            UserId: Guid.CreateVersion7(),
            CategoryId: Guid.CreateVersion7(),
            Amount: 5000m,
            Currency: "RUB",
            Direction: "Credit",
            Description: "Зарплата",
            OccurredAt: FakeDateProvider.Default.UtcNow
        );
    }
}