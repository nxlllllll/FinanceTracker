using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransaction.Jobs;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Application.Workers;

public sealed class RecurringTransactionHandlingJobTests
{
	private ICorrelationContext _correlationContext = null!;
	private IRecurringTransactionReadRepository _readRepository = null!;
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IRabbitMqPublisher _publisher = null!;
	private IJobExecutionContext _jobContext = null!;
	private RecurringTransactionHandlingJob _job = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_correlationContext = Substitute.For<ICorrelationContext>();
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		_readRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_publisher = Substitute.For<IRabbitMqPublisher>();

		_jobContext = Substitute.For<IJobExecutionContext>();
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_job = new RecurringTransactionHandlingJob(
			recurringTransactionReadRepository: _readRepository,
			recurringTransactionWriteRepository: _writeRepository,
			correlationContext: _correlationContext,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<RecurringTransactionHandlingJob>>()
		);
	}
	
	private void SetupEmptyRepository()
	{
		_readRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);
	}

	private void SetupRepository(int count)
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction[] transactions = Enumerable.Range(start: 0, count: count)
			.Select(selector: _ => RecurringTransactionFactory.Create().Value!)
			.ToArray();

		SetupRepository(transactions: transactions);
	}

	private void SetupRepository(FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction[] transactions)
	{
		_readRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transactions);
	}
	
	[Test]
	public async Task Execute_WhenNoDueTransactions_ShouldNotPublish()
	{
		SetupEmptyRepository();

		await _job.Execute(executionContext: _jobContext);

		await _publisher.DidNotReceive().PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenNoDueTransactions_ShouldNotMarkExecuted()
	{
		SetupEmptyRepository();

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishEach()
	{
		SetupRepository(count: 3);

		await _job.Execute(executionContext: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 3).PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishWithCorrectData()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction transaction = RecurringTransactionFactory.Create().Value!;

		SetupRepository(transactions: [transaction]);

		await _job.Execute(executionContext: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 1).PublishAsync(message: Arg.Is<RecurringTransactionTriggeredMessage>(m =>
			m.RecurringTransactionId == transaction.Id &&
			m.AccountId == transaction.AccountId &&
			m.UserId == transaction.UserId &&
			m.CorrelationId == _correlationContext.CorrelationId
		), correlationId: _correlationContext.CorrelationId, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedForEachTransaction()
	{
		SetupRepository(count: 2);

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.Received(requiredNumberOfCalls: 2).MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedWithCorrectTransactionId()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction transaction = RecurringTransactionFactory.Create().Value!;

		SetupRepository(transactions: [transaction]);

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: transaction.Id,
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedWithOccurredAtFromDateProvider()
	{
		SetupRepository(count: 1);

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: FakeDateProvider.Default.UtcNow,
			ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Execute_WhenPublishFails_ShouldNotMarkExecuted()
	{
		SetupRepository(count: 1);

		_publisher.PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => new InvalidOperationException(message: "RabbitMQ unavailable"));

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenPublishFailsOnSecond_ShouldMarkExecutedOnlyForFirst()
	{
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction first = RecurringTransactionFactory.Create().Value!;
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction second = RecurringTransactionFactory.Create().Value!;
		FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction third = RecurringTransactionFactory.Create().Value!;

		SetupRepository(transactions: [first, second, third]);

		int callCount = 0;
		_publisher.PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 2)
				throw new InvalidOperationException(message: "RabbitMQ timeout");
			return Task.CompletedTask;
		});

		await _job.Execute(executionContext: _jobContext);

		await _writeRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: first.Id,
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _writeRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: second.Id,
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: third.Id,
			executedAt: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassCorrectDayOfMonthToRepository()
	{
		SetupEmptyRepository();

		await _job.Execute(executionContext: _jobContext);

		DateTime now = FakeDateProvider.Default.UtcNow;

		await _readRepository.Received(requiredNumberOfCalls: 1).GetDueTodayAsync(
			dayOfMonth: now.Day,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: Arg.Any<DateTime>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassCorrectCurrentMonthStartToRepository()
	{
		SetupEmptyRepository();

		await _job.Execute(executionContext: _jobContext);

		DateTime now = FakeDateProvider.Default.UtcNow;
		DateTime expectedMonthStart = new DateTime(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

		await _readRepository.Received(requiredNumberOfCalls: 1).GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: expectedMonthStart,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassUtcKindCurrentMonthStartToRepository()
	{
		SetupEmptyRepository();
		DateTime? capturedMonthStart = null;

		_readRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Do<DateTime>(x => capturedMonthStart = x),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		await _job.Execute(executionContext: _jobContext);

		await Assert.That(value: capturedMonthStart!.Value.Kind).IsEqualTo(expected: DateTimeKind.Utc);
	}
}