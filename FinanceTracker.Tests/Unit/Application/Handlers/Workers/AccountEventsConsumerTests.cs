using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.AccountProjection.Consumers;
using FinanceTracker.Contracts.Messages.Account;
using FinanceTracker.Worker.AccountProjection.Projection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Workers;

public sealed class AccountEventsConsumerTests : DatabaseFixture
{
    private AccountEventsConsumer _consumer = null!;
    private IUnitOfWork _unitOfWork = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();

        AccountProjection projection = new AccountProjection(
            accountWriteRepository: Substitute.For<IAccountWriteRepository>(),
            logger: Substitute.For<ILogger<AccountProjection>>()
        );

        _consumer = new AccountEventsConsumer(
            projection: projection,
            eventTypeResolver: Substitute.For<IEventTypeResolver>(),
            context: Context,
            unitOfWork: _unitOfWork,
            dateProvider: FakeDateProvider.Default,
            logger: Substitute.For<ILogger<AccountEventsConsumer>>()
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

        AccountEventsMessage message = new AccountEventsMessage(
            MessageId: messageId,
            AggregateId: Guid.CreateVersion7(),
            Events: []
        );

        await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

        await _unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenMessageNotProcessed_ShouldExecuteTransaction()
    {
        AccountEventsMessage message = new AccountEventsMessage(
            MessageId: Guid.CreateVersion7(),
            AggregateId: Guid.CreateVersion7(),
            Events: []
        );

        await _consumer.HandleAsync(message: message, ct: CancellationToken.None);

        await _unitOfWork.Received(requiredNumberOfCalls: 1).ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}