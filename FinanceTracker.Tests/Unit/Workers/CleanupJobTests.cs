using FinanceTracker.Core.Repositories.Idempotency;
using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Core.Repositories.ProcessedMessage;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.Cleanup.Jobs;
using Microsoft.Extensions.Options;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class CleanupJobTests
{
	private IIdempotencyWriteRepository _idempotencyRepo = null!;
	private IOutboxWriteRepository _outboxRepo = null!;
	private IProcessedMessageWriteRepository _processedRepo = null!;
	private IDateProvider _dateProvider = null!;
	private CapturingLogger<CleanupJob> _logger = null!;
	private CleanupJob _job = null!;
	private IJobExecutionContext _jobContext = null!;

	private static readonly DateTime Now = new DateTime(year: 2025, month: 6, day: 1, hour: 12, minute: 0, second: 0, kind: DateTimeKind.Utc);

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
		_idempotencyRepo = Substitute.For<IIdempotencyWriteRepository>();
		_outboxRepo = Substitute.For<IOutboxWriteRepository>();
		_processedRepo = Substitute.For<IProcessedMessageWriteRepository>();
		_dateProvider = Substitute.For<IDateProvider>();
		_logger = new CapturingLogger<CleanupJob>();
		_jobContext = Substitute.For<IJobExecutionContext>();

		_dateProvider.UtcNow.Returns(returnThis: Now);
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_idempotencyRepo.DeleteExpiredAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_outboxRepo.DeleteProcessedAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_outboxRepo.DeleteFailedAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);
		_processedRepo.DeleteOldAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 0);

		_job = new CleanupJob(
			idempotencyRepository: _idempotencyRepo,
			outboxRepository: _outboxRepo,
			processedMessageRepository: _processedRepo,
			dateProvider: _dateProvider,
			options: Options.Create(options: DefaultOptions),
			logger: _logger
		);
	}

	[Test]
	public async Task Execute_IdempotentCommands_DeletesWithCurrentTimeAsCutoff()
	{
		await _job.Execute(context: _jobContext);

		await _idempotencyRepo.Received(requiredNumberOfCalls: 1).DeleteExpiredAsync(
			before: Now,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ProcessedMessages_DeletesWithCorrectRetentionCutoff()
	{
		DateTime expectedCutoff = Now.AddDays(value: -DefaultOptions.ProcessedMessageRetentionDays);

		await _job.Execute(context: _jobContext);

		await _processedRepo.Received(requiredNumberOfCalls: 1).DeleteOldAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_OutboxProcessed_DeletesWithCorrectRetentionCutoff()
	{
		DateTime expectedCutoff = Now.AddDays(value: -DefaultOptions.OutboxProcessedRetentionDays);

		await _job.Execute(context: _jobContext);

		await _outboxRepo.Received(requiredNumberOfCalls: 1).DeleteProcessedAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_OutboxFailed_DeletesWithCorrectRetentionCutoff()
	{
		DateTime expectedCutoff = Now.AddDays(value: -DefaultOptions.OutboxFailedRetentionDays);

		await _job.Execute(context: _jobContext);

		await _outboxRepo.Received(requiredNumberOfCalls: 1).DeleteFailedAsync(
			before: expectedCutoff,
			batchSize: DefaultOptions.BatchSize,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRepositoryReturnsBatchSize_ContinuesDeletingInBatches()
	{
		_idempotencyRepo.DeleteExpiredAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(DefaultOptions.BatchSize, 0);

		await _job.Execute(context: _jobContext);

		await _idempotencyRepo.Received(requiredNumberOfCalls: 2).DeleteExpiredAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRepositoryReturnsLessThanBatchSize_StopsAfterOneBatch()
	{
		_idempotencyRepo.DeleteExpiredAsync(
			before: Arg.Any<DateTime>(), 
			batchSize: Arg.Any<int>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(500);

		await _job.Execute(context: _jobContext);

		await _idempotencyRepo.Received(requiredNumberOfCalls: 1).DeleteExpiredAsync(
			before: Arg.Any<DateTime>(),
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
		_idempotencyRepo.DeleteExpiredAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 10);
		_processedRepo.DeleteOldAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 20);
		_outboxRepo.DeleteProcessedAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 5);
		_outboxRepo.DeleteFailedAsync(
			before: Arg.Any<DateTime>(),
			batchSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 3);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 4);
	}

	[Test]
	public async Task Execute_WhenCancelled_StopsBatchLoop()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		_jobContext.CancellationToken.Returns(returnThis: cts.Token);

		int callCount = 0;
		_idempotencyRepo.DeleteExpiredAsync(
			 before: Arg.Any<DateTime>(), 
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