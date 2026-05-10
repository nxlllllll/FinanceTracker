using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transactions.Services;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransactionProjection.Consumers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Workers;

public sealed class RecurringTransactionConsumerTests : DatabaseFixture
{
    private IAccountRepository _accountRepository = null!;
    private ITransactionCreationService _transactionCreationService = null!;
    private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
    private IRecurringTransactionWriteRepository _recurringTransactionWriteRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private RecurringTransactionConsumer _consumer = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _accountRepository = Substitute.For<IAccountRepository>();
        _transactionCreationService = Substitute.For<ITransactionCreationService>();
        _recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();
        _recurringTransactionWriteRepository = Substitute.For<IRecurringTransactionWriteRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();

        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(callInfo => callInfo.Arg<Func<Task>>()());

        _consumer = new RecurringTransactionConsumer(
            accountRepository: _accountRepository,
            transactionCreationService: _transactionCreationService,
            recurringTransactionReadRepository: _recurringTransactionReadRepository,
            recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
            unitOfWork: _unitOfWork,
            context: Context,
            dateProvider: FakeDateProvider.Default,
            logger: Substitute.For<ILogger<RecurringTransactionConsumer>>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenMessageAlreadyProcessed_ShouldSkip()
    {
        Guid messageId = Guid.CreateVersion7();

        await Context.ProcessedMessages.AddAsync(entity: new ProcessedMessageEntity
        {
            MessageId = messageId,
            ProcessedAt = FakeDateProvider.Default.UtcNow
        });
        await Context.SaveChangesAsync();

        await _consumer.HandleAsync(message: BuildMessage(messageId: messageId), ct: CancellationToken.None);

        await _transactionCreationService.DidNotReceive().CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );

        await _recurringTransactionWriteRepository.DidNotReceive().MarkExecutedAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            executedAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenRecurringTransactionNotFound_ShouldSkip()
    {
        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction?)null);

        await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

        await _transactionCreationService.DidNotReceive().CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenAccountNotFound_ShouldThrow()
    {
        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: RecurringTransactionFactory.Create().Value!);

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: (FinanceTracker.Core.Domains.Account.Account?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            action: async () => await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None)
        );
    }

    [Test]
    public async Task HandleAsync_WhenValid_ShouldCreateTransactionAndMarkExecuted()
    {
        _recurringTransactionReadRepository.GetByIdAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: RecurringTransactionFactory.Create().Value!);

        _accountRepository.GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: AccountFactory.Create().Value!);

        _transactionCreationService.CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Result<Guid, DomainException>.Success(value: Guid.CreateVersion7()));

        await _consumer.HandleAsync(message: BuildMessage(), ct: CancellationToken.None);

        await _transactionCreationService.Received(requiredNumberOfCalls: 1).CreateAsync(
            command: Arg.Any<CreateTransactionCommand>(),
            account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
            ct: Arg.Any<CancellationToken>()
        );

        await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
            recurringTransactionId: Arg.Any<Guid>(),
            executedAt: Arg.Any<DateTime>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    private static RecurringTransactionTriggeredMessage BuildMessage(Guid? messageId = null)
    {
        return new RecurringTransactionTriggeredMessage(
            MessageId: messageId ?? Guid.CreateVersion7(),
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