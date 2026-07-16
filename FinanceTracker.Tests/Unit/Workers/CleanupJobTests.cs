using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Repositories.Snapshot;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Cleanup.Job;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class CleanupJobTests
{
	private IAccountWriteRepository _accountWriteRepository = null!;
	private IIdempotencyWriteRepository _idempotencyWriteRepository = null!;
	private IOutboxWriteRepository _outboxWriteRepository = null!;
	private IProcessedMessageWriteRepository _processedMessageWriteRepository = null!;
	private ISnapshotWriteRepository _snapshotWriteRepository = null!;
	private IDateProvider _dateProvider = null!;
	private CapturingLogger<CleanupJob> _logger = null!;
	private CleanupJob _job = null!;
	private IJobExecutionContext _jobContext = null!;

	private static readonly DateTimeOffset Now = new DateTimeOffset(year: 2025, month: 6, day: 1, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

	private static readonly CleanupOptions DefaultOptions = new CleanupOptions
	{
		BatchSize = 1000,
		ProcessedMessageRetentionDays = 30,
		OutboxProcessedRetentionDays = 7,
		OutboxFailedRetentionDays = 30
	};

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();
		_idempotencyWriteRepository = Substitute.For<IIdempotencyWriteRepository>();
		_outboxWriteRepository = Substitute.For<IOutboxWriteRepository>();
		_processedMessageWriteRepository = Substitute.For<IProcessedMessageWriteRepository>();
		_snapshotWriteRepository = Substitute.For<ISnapshotWriteRepository>();
		_dateProvider = Substitute.For<IDateProvider>();
		_logger = new CapturingLogger<CleanupJob>();
		_jobContext = Substitute.For<IJobExecutionContext>();

		_dateProvider.UtcNow.Returns(returnThis: Now);
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_idempotencyWriteRepository.DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_outboxWriteRepository.DeleteProcessedAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_outboxWriteRepository.DeleteFailedAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_processedMessageWriteRepository.DeleteOldAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_snapshotWriteRepository.DeleteOldAsync(
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);

		_job = new CleanupJob(
			idempotencyRepository: _idempotencyWriteRepository,
			accountWriteRepository: _accountWriteRepository,
			outboxRepository: _outboxWriteRepository,
			processedMessageRepository: _processedMessageWriteRepository,
			snapshotRepository: _snapshotWriteRepository,
			dateProvider: _dateProvider,
			options: new FakeOptionsMonitor<CleanupOptions>(value: DefaultOptions),
			logger: _logger
		);
	}

	[Test]
	public async Task Execute_IdempotentCommands_DeletesWithCurrentTimeAsCutoff()
	{
		await _job.Execute(context: _jobContext);

		await _idempotencyWriteRepository.Received(requiredNumberOfCalls: 1).DeleteExpiredAsync(
			before: Now,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ProcessedMessages_DeletesWithCorrectRetentionCutoff()
	{
		DateTimeOffset expectedCutoff = Now.AddDays(days: -DefaultOptions.ProcessedMessageRetentionDays);

		await _job.Execute(context: _jobContext);

		await _processedMessageWriteRepository.Received(requiredNumberOfCalls: 1).DeleteOldAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_OutboxProcessed_DeletesWithCorrectRetentionCutoff()
	{
		DateTimeOffset expectedCutoff = Now.AddDays(days: -DefaultOptions.OutboxProcessedRetentionDays);

		await _job.Execute(context: _jobContext);

		await _outboxWriteRepository.Received(requiredNumberOfCalls: 1).DeleteProcessedAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_OutboxFailed_DeletesWithCorrectRetentionCutoff()
	{
		DateTimeOffset expectedCutoff = Now.AddDays(days: -DefaultOptions.OutboxFailedRetentionDays);

		await _job.Execute(context: _jobContext);

		await _outboxWriteRepository.Received(requiredNumberOfCalls: 1).DeleteFailedAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_Snapshots_CallsDeleteOldWithBatchSize()
	{
		await _job.Execute(context: _jobContext);

		await _snapshotWriteRepository.Received(requiredNumberOfCalls: 1).DeleteOldAsync(
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_Snapshots_WhenRowsDeleted_Logs()
	{
		_snapshotWriteRepository.DeleteOldAsync(
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 5);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Execute_Snapshots_WhenReturnsBatchSize_ContinuesDeletingInBatches()
	{
		_snapshotWriteRepository.DeleteOldAsync(
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: DefaultOptions.BatchSize, returnThese: 0);

		await _job.Execute(context: _jobContext);

		await _snapshotWriteRepository.Received(requiredNumberOfCalls: 2).DeleteOldAsync(
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRepositoryReturnsBatchSize_ContinuesDeletingInBatches()
	{
		_idempotencyWriteRepository.DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: DefaultOptions.BatchSize, returnThese: 0);

		await _job.Execute(context: _jobContext);

		await _idempotencyWriteRepository.Received(requiredNumberOfCalls: 2).DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRepositoryReturnsLessThanBatchSize_StopsAfterOneBatch()
	{
		_idempotencyWriteRepository.DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 500);

		await _job.Execute(context: _jobContext);

		await _idempotencyWriteRepository.Received(requiredNumberOfCalls: 1).DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenNothingDeleted_DoesNotLog()
	{
		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Execute_WhenRowsDeleted_LogsForEachTable()
	{
		_idempotencyWriteRepository.DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 10);
		_processedMessageWriteRepository.DeleteOldAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 20);
		_outboxWriteRepository.DeleteProcessedAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 5);
		_outboxWriteRepository.DeleteFailedAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 3);
		_snapshotWriteRepository.DeleteOldAsync(
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 7);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 5);
	}

	[Test]
	public async Task Execute_WhenCancelled_StopsBatchLoop()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		_jobContext.CancellationToken.Returns(returnThis: cts.Token);

		int callCount = 0;
		_idempotencyWriteRepository.DeleteExpiredAsync(
			before: Arg.Any<DateTimeOffset>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			if (++callCount == 1)
				cts.Cancel();
			return DefaultOptions.BatchSize;
		});

		await _job.Execute(context: _jobContext);

		await Assert.That(value: callCount).IsEqualTo(expected: 1);
	}
}
