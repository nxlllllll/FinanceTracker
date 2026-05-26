using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.DomainEventOutbox.Job;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class DomainEventOutboxPublisherJobTests
{
    private IDomainOutboxReadRepository _readRepository = null!;
    private IDomainOutboxWriteRepository _writeRepository = null!;
    private IUnresolvableEventWriteRepository _unresolvableEventWriteRepository = null!;
    private IRabbitMqPublisher _publisher = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IDateProvider _dateProvider = null!;
    private IJobExecutionContext _jobContext = null!;
    private DomainEventOutboxPublisherJob _job = null!;

    private static readonly DateTimeOffset Now = new DateTimeOffset(year: 2025, month: 6, day: 1, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

    private static readonly DomainEventOutboxPublisherJobOptions DefaultOptions = new DomainEventOutboxPublisherJobOptions
    {
        IsEnabled = true,
        BatchSize = 10,
        MaxRetries = 3
    };

    private static PendingDomainEvent MakeEvent(int retryCount = 0)
    {
        return new PendingDomainEvent(
            Id: Guid.CreateVersion7(),
            EventType: "account.created",
            AggregateId: Guid.CreateVersion7(),
            AggregateType: "Account",
            CorrelationId: Guid.CreateVersion7(),
            Payload: "{}",
            OccurredAt: Now,
            RetryCount: retryCount
        );
    }

    [Before(hookType: Test)]
    public void Setup()
    {
        _readRepository = Substitute.For<IDomainOutboxReadRepository>();
        _writeRepository = Substitute.For<IDomainOutboxWriteRepository>();
        _unresolvableEventWriteRepository = Substitute.For<IUnresolvableEventWriteRepository>();
        _publisher = Substitute.For<IRabbitMqPublisher>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _dateProvider = Substitute.For<IDateProvider>();
        _jobContext = Substitute.For<IJobExecutionContext>();

        _dateProvider.UtcNow.Returns(returnThis: Now);
        _jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: call => call.Arg<Func<Task>>()());

        _readRepository.GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        _job = new DomainEventOutboxPublisherJob(
            readRepository: _readRepository,
            writeRepository: _writeRepository,
            unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
            publisher: _publisher,
            unitOfWork: _unitOfWork,
            dateProvider: _dateProvider,
            options: new FakeOptionsMonitor<DomainEventOutboxPublisherJobOptions>(value: DefaultOptions),
            logger: new CapturingLogger<DomainEventOutboxPublisherJob>()
        );
    }

    [Test]
    public async Task Execute_WhenDisabled_ShouldNotReadBatch()
    {
        DomainEventOutboxPublisherJob disabledJob = new DomainEventOutboxPublisherJob(
            readRepository: _readRepository,
            writeRepository: _writeRepository,
            unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
            publisher: _publisher,
            unitOfWork: _unitOfWork,
            dateProvider: _dateProvider,
            options: new FakeOptionsMonitor<DomainEventOutboxPublisherJobOptions>(
                value: new DomainEventOutboxPublisherJobOptions { IsEnabled = false }
            ),
            logger: new CapturingLogger<DomainEventOutboxPublisherJob>()
        );
 
        await disabledJob.Execute(executionContext: _jobContext);
 
        await _readRepository.DidNotReceive().GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenBatchIsEmpty_ShouldNotPublish()
    {
        await _job.Execute(executionContext: _jobContext);

        await _publisher.DidNotReceive().PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenBatchHasEvents_ShouldMarkEachAsProcessed()
    {
        PendingDomainEvent event1 = MakeEvent();
        PendingDomainEvent event2 = MakeEvent();

        _readRepository.GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [event1, event2]);

        await _job.Execute(executionContext: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsProcessedAsync(
            id: event1.Id,
            processedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsProcessedAsync(
            id: event2.Id,
            processedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenPublishFails_ShouldIncrementRetryCount()
    {
        PendingDomainEvent @event = MakeEvent(retryCount: 0);

        _readRepository.GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [@event]);

        _publisher.PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InvalidOperationException(message: "broker unavailable"));

        await _job.Execute(executionContext: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsFailedAsync(
            id: @event.Id,
            retryCount: 1,
            failedAt: null,
            ct: Arg.Any<CancellationToken>()
        );
        await _unresolvableEventWriteRepository.DidNotReceive().CreateAsync(
            type: Arg.Any<UnresolvableEventType>(),
            referenceId: Arg.Any<Guid>(),
            reason: Arg.Any<string>(),
            payload: Arg.Any<string>(),
            occurredAt: Arg.Any<DateTimeOffset>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenMaxRetriesExceeded_ShouldMoveToUnresolvableEvents()
    {
        PendingDomainEvent @event = MakeEvent(retryCount: DefaultOptions.MaxRetries - 1);

        _readRepository.GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [@event]);

        _publisher.PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InvalidOperationException(message: "broker unavailable"));

        await _job.Execute(executionContext: _jobContext);

        await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            type: UnresolvableEventType.OutboxDeadLetter,
            referenceId: @event.Id,
            reason: Arg.Any<string>(),
            payload: Arg.Any<string>(),
            occurredAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsFailedAsync(
            id: @event.Id,
            retryCount: DefaultOptions.MaxRetries,
            failedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenCancelled_ShouldStopProcessingBatch()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        _jobContext.CancellationToken.Returns(returnThis: cts.Token);

        PendingDomainEvent event1 = MakeEvent();
        PendingDomainEvent event2 = MakeEvent();

        _readRepository.GetPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [event1, event2]);

        _publisher.PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: _ =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        });

        await _job.Execute(executionContext: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsProcessedAsync(
            id: Arg.Any<Guid>(),
            processedAt: Arg.Any<DateTimeOffset>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}
