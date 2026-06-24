using System.Text.Json;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Outbox.Job;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class OutboxPublisherJobTests
{
    private IOutboxReadRepository _readRepository = null!;
    private IOutboxWriteRepository _writeRepository = null!;
    private IUnresolvableEventWriteRepository _unresolvableEventWriteRepository = null!;
    private IRabbitMqPublisher _publisher = null!;
    private IUnitOfWork _unitOfWork = null!;
    private IDateProvider _dateProvider = null!;
    private IJobExecutionContext _jobContext = null!;
    private OutboxPublisherJob _job = null!;

    private static readonly DateTimeOffset Now = new DateTimeOffset(year: 2025, month: 6, day: 1, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

    private static readonly OutboxOptions DefaultOptions = new OutboxOptions
    {
        IsEnabled = true,
        BatchSize = 10,
        MaxRetries = 3,
        LeaseDurationSeconds = 60
    };

    private static PendingOutboxMessage MakeMessage(int retryCount = 0)
    {
        string payload = JsonSerializer.Serialize(value: new OutboxPayload(
            AggregateId: Guid.CreateVersion7(),
            CorrelationId: Guid.CreateVersion7(),
            Events: [new OutboxEventEnvelope(EventType: "account.created", EventPayload: "{}")]
        ));

        return new PendingOutboxMessage(
            Id: Guid.CreateVersion7(),
            AggregateId: Guid.CreateVersion7(),
            AggregateType: AggregateTypeNames.Account,
            Payload: payload,
            RetryCount: retryCount
        );
    }

    [Before(hookType: Test)]
    public void Setup()
    {
        _readRepository = Substitute.For<IOutboxReadRepository>();
        _writeRepository = Substitute.For<IOutboxWriteRepository>();
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

        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: call => call.Arg<Func<Task>>()());

        _readRepository.ClaimPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            now: Arg.Any<DateTimeOffset>(),
            leaseDuration: Arg.Any<TimeSpan>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        _job = new OutboxPublisherJob(
            outboxReadRepository: _readRepository,
            outboxWriteRepository: _writeRepository,
            unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
            publisher: _publisher,
            unitOfWork: _unitOfWork,
            dateProvider: _dateProvider,
            options: new FakeOptionsMonitor<OutboxOptions>(value: DefaultOptions),
            logger: new CapturingLogger<OutboxPublisherJob>()
        );
    }
    [Test]
    public async Task Execute_WhenDisabled_ShouldNotReadBatch()
    {
        OutboxPublisherJob disabledJob = new OutboxPublisherJob(
            outboxReadRepository: _readRepository,
            outboxWriteRepository: _writeRepository,
            unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
            publisher: _publisher,
            unitOfWork: _unitOfWork,
            dateProvider: _dateProvider,
            options: new FakeOptionsMonitor<OutboxOptions>(
                value: new OutboxOptions { IsEnabled = false }
            ),
            logger: new CapturingLogger<OutboxPublisherJob>()
        );
 
        await disabledJob.Execute(context: _jobContext);
 
        await _readRepository.DidNotReceive().ClaimPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            now: Arg.Any<DateTimeOffset>(),
            leaseDuration: Arg.Any<TimeSpan>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenBatchIsEmpty_ShouldNotPublish()
    {
        await _job.Execute(context: _jobContext);

        await _publisher.DidNotReceive().PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenBatchHasMessages_ShouldMarkEachAsPublished()
    {
        PendingOutboxMessage msg1 = MakeMessage();
        PendingOutboxMessage msg2 = MakeMessage();

        _readRepository.ClaimPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            now: Arg.Any<DateTimeOffset>(),
            leaseDuration: Arg.Any<TimeSpan>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [msg1, msg2]);

        await _job.Execute(context: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsPublishedAsync(
            messageId: msg1.Id,
            processedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsPublishedAsync(
            messageId: msg2.Id,
            processedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_ShouldClaimUsingConfiguredLeaseDuration()
    {
        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).ClaimPendingBatchAsync(
            batchSize: DefaultOptions.BatchSize,
            now: Now,
            leaseDuration: TimeSpan.FromSeconds(value: DefaultOptions.LeaseDurationSeconds),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenPublishFails_ShouldIncrementRetryCount()
    {
        PendingOutboxMessage message = MakeMessage(retryCount: 0);

        _readRepository.ClaimPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            now: Arg.Any<DateTimeOffset>(),
            leaseDuration: Arg.Any<TimeSpan>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [message]);

        _publisher.PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InvalidOperationException(message: "broker unavailable"));

        await _job.Execute(context: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsFailedAsync(
            messageId: message.Id,
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
        PendingOutboxMessage message = MakeMessage(retryCount: DefaultOptions.MaxRetries - 1);

        _readRepository.ClaimPendingBatchAsync(
            batchSize: Arg.Any<int>(),
            now: Arg.Any<DateTimeOffset>(),
            leaseDuration: Arg.Any<TimeSpan>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [message]);

        _publisher.PublishAsync(
            message: Arg.Any<IRoutableMessage>(),
            correlationId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InvalidOperationException(message: "broker unavailable"));

        await _job.Execute(context: _jobContext);

        await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            type: UnresolvableEventType.OutboxDeadLetter,
            referenceId: message.Id,
            reason: Arg.Any<string>(),
            payload: Arg.Any<string>(),
            occurredAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
        await _writeRepository.Received(requiredNumberOfCalls: 1).MarkAsFailedAsync(
            messageId: message.Id,
            retryCount: DefaultOptions.MaxRetries,
            failedAt: Now,
            ct: Arg.Any<CancellationToken>()
        );
    }
}