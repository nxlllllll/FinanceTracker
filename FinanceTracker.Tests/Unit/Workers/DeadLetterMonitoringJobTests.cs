using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.DeadLetterMonitor.Job;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class DeadLetterMonitoringJobTests
{
    private IUnresolvableEventReadRepository _readRepository = null!;
    private IUnresolvableEventWriteRepository _writeRepository = null!;
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
        _writeRepository = Substitute.For<IUnresolvableEventWriteRepository>();
        _logger = new CapturingLogger<DeadLetterMonitoringJob>();
        _jobContext = Substitute.For<IJobExecutionContext>();

        _jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

        _job = new DeadLetterMonitoringJob(
            unresolvableEventReadRepository: _readRepository,
            unresolvableEventWriteRepository: _writeRepository,
            dateProvider: FakeDateProvider.Default,
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
            OccurredAt: occurredAt ?? FakeDateProvider.Default.UtcNow,
            AcknowledgedAt: null,
            ResolvedAt: null
        );
    }

    private static PagedResult<UnresolvableEvent> BuildPage(IReadOnlyList<UnresolvableEvent> items, bool hasNextPage = false)
        => new PagedResult<UnresolvableEvent>(Items: items, HasNextPage: hasNextPage, NextCursorDate: null, NextCursorId: null);

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldNotLog()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: []));

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldCallRepositoryOnce()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: []));

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldNotAcknowledgeAnything()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: []));

        await _job.Execute(context: _jobContext);

        await _writeRepository.DidNotReceive().AcknowledgeBatchAsync(
            ids: Arg.Any<IReadOnlyList<Guid>>(),
            acknowledgedAt: Arg.Any<DateTimeOffset>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_ShouldPassBatchSizeFromOptions()
    {
        DeadLetterMonitoringOptions customOptions = new DeadLetterMonitoringOptions { BatchSize = 50 };
        DeadLetterMonitoringJob job = new DeadLetterMonitoringJob(
            unresolvableEventReadRepository: _readRepository,
            unresolvableEventWriteRepository: _writeRepository,
            dateProvider: FakeDateProvider.Default,
            options: new FakeOptionsMonitor<DeadLetterMonitoringOptions>(customOptions),
            logger: _logger
        );

        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: []));

        await job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetUnacknowledgedBatchAsync(
            batchSize: 50,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenSinglePageWithoutMore_ShouldCallRepositoryOnce()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: [BuildDto(), BuildDto()], hasNextPage: false));

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenHasNextPage_ShouldCallRepositoryAgain()
    {
        UnresolvableEvent e1 = BuildDto();
        UnresolvableEvent e2 = BuildDto();

        int callCount = 0;
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: _ =>
        {
            callCount++;
            return callCount == 1
                ? BuildPage(items: [e1, e2], hasNextPage: true)
                : BuildPage(items: [], hasNextPage: false);
        });

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 2).GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenEventsExist_ShouldAcknowledgeThem()
    {
        UnresolvableEvent e1 = BuildDto();
        UnresolvableEvent e2 = BuildDto();

        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: [e1, e2]));

        await _job.Execute(context: _jobContext);

        await _writeRepository.Received(requiredNumberOfCalls: 1).AcknowledgeBatchAsync(
            ids: Arg.Is<IReadOnlyList<Guid>>(predicate: ids => ids.Contains(e1.Id) && ids.Contains(e2.Id) && ids.Count == 2),
            acknowledgedAt: Arg.Any<DateTimeOffset>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenEventsExist_ShouldLogWarning()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: [BuildDto(), BuildDto()]));

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.WarningLogged).IsTrue();
    }

    [Test]
    public async Task Execute_WhenSingleEvent_ShouldLogSummaryAndEvent()
    {
        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: [BuildDto()]));

        await _job.Execute(context: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task Execute_PassesCancellationTokenToRepository()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        _jobContext.CancellationToken.Returns(returnThis: cts.Token);

        _readRepository.GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: BuildPage(items: []));

        await _job.Execute(context: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetUnacknowledgedBatchAsync(
            batchSize: Arg.Any<int>(),
            ct: cts.Token
        );
    }
}