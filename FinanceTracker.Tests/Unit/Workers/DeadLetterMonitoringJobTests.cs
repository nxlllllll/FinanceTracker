using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
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

    [Before(hookType: Test)]
    public void Setup()
    {
        _readRepository = Substitute.For<IUnresolvableEventReadRepository>();
        _logger = new CapturingLogger<DeadLetterMonitoringJob>();
        _jobContext = Substitute.For<IJobExecutionContext>();

        _jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

        _job = new DeadLetterMonitoringJob(
            unresolvableEventReadRepository: _readRepository,
            options: new FakeOptionsMonitor<DeadLetterMonitoringOptions>(new DeadLetterMonitoringOptions()),
            logger: _logger
        );
    }

    private static UnresolvableEvent BuildDto(UnresolvableEventType type = UnresolvableEventType.OutboxDeadLetter)
    {
        return new UnresolvableEvent(
            Id: Guid.CreateVersion7(),
            Type: type,
            ReferenceId: Guid.CreateVersion7(),
            Reason: "Max retries exceeded.",
            OccurredAt: FakeDateProvider.Default.UtcNow
        );
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldNotLog()
    {
        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);

        await _job.Execute(executionContext: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task Execute_WhenNoUnresolvableEvents_ShouldCallRepository()
    {
        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);

        await _job.Execute(executionContext: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Execute_WhenUnresolvableEventsExist_ShouldLogWarningForEach()
    {
        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis:
        [
            BuildDto(type: UnresolvableEventType.OutboxDeadLetter),
            BuildDto(type: UnresolvableEventType.TransferCompensation),
            BuildDto(type: UnresolvableEventType.OutboxDeadLetter)
        ]);

        await _job.Execute(executionContext: _jobContext);

        await Assert.That(value: _logger.WarningLogged).IsTrue();
    }

    [Test]
    public async Task Execute_WhenUnresolvableEventsExist_ShouldLogCountPlusSummary()
    {
        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis:
        [
            BuildDto(),
            BuildDto(),
            BuildDto()
        ]);

        await _job.Execute(executionContext: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 4);
    }

    [Test]
    public async Task Execute_WhenSingleEvent_ShouldLogTwice()
    {
        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [BuildDto()]);

        await _job.Execute(executionContext: _jobContext);

        await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task Execute_PassesCancellationTokenToRepository()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        _jobContext.CancellationToken.Returns(returnThis: cts.Token);

        _readRepository.GetAllAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);

        await _job.Execute(executionContext: _jobContext);

        await _readRepository.Received(requiredNumberOfCalls: 1).GetAllAsync(ct: cts.Token);
    }
}
