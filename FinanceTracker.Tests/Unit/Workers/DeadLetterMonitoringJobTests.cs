using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.DeadLetterMonitor.Job;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class DeadLetterMonitoringJobTests
{
    private IUnresolvableEventReadRepository _readRepository = null!;
    private CapturingLogger<DeadLetterMonitoringJob> _logger = null!;
    private IJobExecutionContext _jobContext = null!;
    private DeadLetterMonitoringJob _job = null!;

    private static readonly DeadLetterMonitoringOptions DefaultOptions = new DeadLetterMonitoringOptions
    {
        BatchSize = 3
    };

    [Before(hookType: Test)]
    public void Setup()
    {
        _readRepository = Substitute.For<IUnresolvableEventReadRepository>();
        _logger = new CapturingLogger<DeadLetterMonitoringJob>();
        _jobContext = Substitute.For<IJobExecutionContext>();

        _jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

        _job = new DeadLetterMonitoringJob(
            unresolvableEventReadRepository: _readRepository,
            options: new FakeOptionsMonitor<DeadLetterMonitoringOptions>(DefaultOptions),
            logger: _logger
        );
    }

    private static UnresolvableEvent BuildDto(
        UnresolvableEventType type = UnresolvableEventType.OutboxDeadLetter,
        DateTimeOffset? occurredAt = null)
    {
        return new UnresolvableEvent(
            Id: Guid.CreateVersion7(),
            Type: type,
            ReferenceId: Guid.CreateVersion7(),
            Reason: "Max retries exceeded.",
            OccurredAt: occurredAt ?? FakeDateProvider.Default.UtcNow
        );
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldNotLog()
    {
        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldCallRepositoryOnce()
    {
        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: null,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_ShouldPassBatchSizeFromOptions()
    {
        DeadLetterMonitoringOptions customOptions = new DeadLetterMonitoringOptions { BatchSize = 50 };
        DeadLetterMonitoringJob job = new DeadLetterMonitoringJob(
            unresolvableEventReadRepository: _readRepository,
            options: new FakeOptionsMonitor<DeadLetterMonitoringOptions>(customOptions),
            logger: _logger
        );

        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetBatchAsync(
            batchSize: 50,
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenSinglePartialBatch_ShouldCallRepositoryTwice()
    {
        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [BuildDto(), BuildDto()]);

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenMultipleFullBatches_ShouldPaginateUntilEmpty()
    {
        DateTimeOffset t1 = FakeDateProvider.Default.UtcNow;
        DateTimeOffset t2 = t1.AddSeconds(seconds: 1);
        DateTimeOffset t3 = t1.AddSeconds(seconds: 2);

        UnresolvableEvent e1 = BuildDto(occurredAt: t1);
        UnresolvableEvent e2 = BuildDto(occurredAt: t1.AddMilliseconds(milliseconds: 1));
        UnresolvableEvent e3 = BuildDto(occurredAt: t1.AddMilliseconds(milliseconds: 2));
        UnresolvableEvent e4 = BuildDto(occurredAt: t2);
        UnresolvableEvent e5 = BuildDto(occurredAt: t3);

        _readRepository.GetBatchAsync(
            batchSize: 3,
            cursor: null,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [e1, e2, e3]);

        _readRepository.GetBatchAsync(
            batchSize: 3,
            cursor: e3.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [e4, e5]);

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 2).GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenMultipleFullBatches_ShouldPassCursorFromLastEvent()
    {
        DateTimeOffset t1 = FakeDateProvider.Default.UtcNow;

        UnresolvableEvent e1 = BuildDto(occurredAt: t1);
        UnresolvableEvent e2 = BuildDto(occurredAt: t1.AddMilliseconds(milliseconds: 1));
        UnresolvableEvent e3 = BuildDto(occurredAt: t1.AddMilliseconds(milliseconds: 2));

        _readRepository.GetBatchAsync(
            batchSize: 3,
            cursor: null,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [e1, e2, e3]);

        _readRepository.GetBatchAsync(
            batchSize: 3,
            cursor: e3.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetBatchAsync(
            batchSize: 3,
            cursor: e3.OccurredAt,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenEventsExist_ShouldLogWarning()
    {
        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [BuildDto(), BuildDto()]);

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.WarningLogged).IsTrue();
    }

    [Test]
    public async Task Execute_WhenSingleEvent_ShouldLogSummaryAndEvent()
    {
        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: [BuildDto()]);

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task Execute_PassesCancellationTokenToRepository()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        _jobContext.CancellationToken.Returns(returnThis: cts.Token);

        _readRepository.GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: []);

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetBatchAsync(
            batchSize: Arg.Any<int>(),
            cursor: Arg.Any<DateTimeOffset?>(),
            ct: cts.Token
        );
    }
}