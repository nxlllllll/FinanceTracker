using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Utilities;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.RecurringTransaction.Job;
using FinanceTracker.Worker.Shared.RabbitMQ.Publisher;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class RecurringTransactionHandlingJobTests
{
	private ICorrelationContext _correlationContext = null!;
	private IRecurringTransactionReadRepository _recurringTransactionReadRepository = null!;
	private IRecurringTransactionWriteRepository _recurringTransactionWriteRepository = null!;
	private IUnresolvableEventWriteRepository _unresolvableEventWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IRabbitMqPublisher _publisher = null!;
	private IJobExecutionContext _jobContext = null!;
	private RecurringTransactionHandlingJob _job = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_correlationContext = Substitute.For<ICorrelationContext>();
		_correlationContext.CorrelationId.Returns(returnThis: Guid.CreateVersion7());
		_recurringTransactionReadRepository = Substitute.For<IRecurringTransactionReadRepository>();
		_recurringTransactionWriteRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_unresolvableEventWriteRepository = Substitute.For<IUnresolvableEventWriteRepository>();
		_publisher = Substitute.For<IRabbitMqPublisher>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_jobContext = Substitute.For<IJobExecutionContext>();
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()?.Invoke());

		SetupNoMissedTransactions();

		_job = new RecurringTransactionHandlingJob(
			recurringTransactionReadRepository: _recurringTransactionReadRepository,
			recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
			unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
			unitOfWork: _unitOfWork,
			correlationContext: _correlationContext,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<RecurringTransactionJobOptions>(value: new RecurringTransactionJobOptions()),
			logger: Substitute.For<ILogger<RecurringTransactionHandlingJob>>()
		);
	}

	private void SetupNoMissedTransactions()
	{
		_recurringTransactionReadRepository.GetMissedThisMonthAsync(
			dayOfMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTimeOffset>(),
			previousMonthStart: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);
	}

	private void SetupEmptyRepository()
	{
		_recurringTransactionReadRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);
	}

	private void SetupRepository(int count)
	{
		IReadOnlyList<RecurringTransactionReadModel> transactions = [.. Enumerable.Range(start: 0, count: count).Select(selector: _ => RecurringTransactionFactory.CreateReadModel())];

		SetupRepository(transactions: transactions);
	}

	private void SetupRepository(IReadOnlyList<RecurringTransactionReadModel> transactions)
	{
		_recurringTransactionReadRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transactions);
	}

	private void SetupMissedTransactions(IReadOnlyList<RecurringTransactionReadModel> transactions)
	{
		_recurringTransactionReadRepository.GetMissedThisMonthAsync(
			dayOfMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Any<DateTimeOffset>(),
			previousMonthStart: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transactions);
	}

	[Test]
	public async Task Execute_WhenNoDueTransactions_ShouldNotPublish()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		await _publisher.DidNotReceive().PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenNoDueTransactions_ShouldNotMarkExecuted()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishEach()
	{
		SetupRepository(count: 3);

		await _job.Execute(context: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 3).PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishWithCorrectData()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		await _job.Execute(context: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 1).PublishAsync(message: Arg.Is<RecurringTransactionTriggeredMessage>(m =>
			m!.RecurringTransactionId == transaction.Id &&
			m.AccountId == transaction.AccountId &&
			m.UserId == transaction.UserId &&
			m.CorrelationId == _correlationContext.CorrelationId
		), correlationId: _correlationContext.CorrelationId, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishWithDeterministicMessageId()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		DateTimeOffset now = FakeDateProvider.Default.UtcNow;
		Guid expectedMessageId = DeterministicGuid.Create(baseId: transaction.Id, year: now.Year, month: now.Month);

		await _job.Execute(context: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 1).PublishAsync(
			message: Arg.Is<RecurringTransactionTriggeredMessage>(predicate: m => m!.MessageId == expectedMessageId),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPublishBeforeMarkExecuted()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		List<string> callOrder = [];

		_publisher.PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			callOrder.Add(item: "Publish");
			return Task.CompletedTask;
		});

		_recurringTransactionWriteRepository.MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			callOrder.Add(item: "MarkExecuted");
			return Task.CompletedTask;
		});

		await _job.Execute(context: _jobContext);

		await Assert.That(value: callOrder).IsEquivalentTo(expected: ["Publish", "MarkExecuted"]);
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedForEachTransaction()
	{
		SetupRepository(count: 2);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 2).MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedWithCorrectTransactionId()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: transaction.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldMarkExecutedWithOccurredAtFromDateProvider()
	{
		SetupRepository(count: 1);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: FakeDateProvider.Default.UtcNow,
			expectedVersion: Arg.Any<int>(),
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

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: Arg.Any<Guid>(),
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenPublishFails_ShouldNotEscalateImmediately()
	{
		SetupRepository(count: 1);

		_publisher.PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => new InvalidOperationException(message: "RabbitMQ unavailable"));

		await _job.Execute(context: _jobContext);

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
	public async Task Execute_WhenPublishFailsOnSecond_ShouldMarkExecutedOnlyForFirst()
	{
		RecurringTransactionReadModel first = RecurringTransactionFactory.CreateReadModel();
		RecurringTransactionReadModel second = RecurringTransactionFactory.CreateReadModel();
		RecurringTransactionReadModel third = RecurringTransactionFactory.CreateReadModel();

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

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: first.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recurringTransactionWriteRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: second.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: third.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassCorrectDayOfMonthToRepository()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		DateTimeOffset now = FakeDateProvider.Default.UtcNow;

		await _recurringTransactionReadRepository.Received(requiredNumberOfCalls: 1).GetDueTodayAsync(
			dayOfMonth: now.Day,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassCorrectCurrentMonthStartToRepository()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		DateTimeOffset now = FakeDateProvider.Default.UtcNow;
		DateTimeOffset expectedMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		await _recurringTransactionReadRepository.Received(requiredNumberOfCalls: 1).GetDueTodayAsync(
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
		DateTimeOffset? capturedMonthStart = null;

		_recurringTransactionReadRepository.GetDueTodayAsync(
			dayOfMonth: Arg.Any<int>(),
			daysInCurrentMonth: Arg.Any<int>(),
			currentMonthStart: Arg.Do<DateTimeOffset>(useArgument: x => capturedMonthStart = x),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: capturedMonthStart!.Value.Offset).IsEqualTo(expected: TimeSpan.Zero);
	}

	[Test]
	public async Task Execute_WhenNoMissedTransactions_ShouldNotEscalate()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

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
	public async Task Execute_WhenMissedTransactionsExist_ShouldEscalateEach()
	{
		SetupEmptyRepository();
		SetupMissedTransactions(transactions: [
			RecurringTransactionFactory.CreateReadModel(),
			RecurringTransactionFactory.CreateReadModel()
		]);

		await _job.Execute(context: _jobContext);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 2).CreateAsync(
			type: UnresolvableEventType.RecurringTransactionFailed,
			referenceId: Arg.Any<Guid>(),
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenMissedTransactionsExist_ShouldEscalateWithCorrectReferenceId()
	{
		RecurringTransactionReadModel missed = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupMissedTransactions(transactions: [missed]);

		await _job.Execute(context: _jobContext);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.RecurringTransactionFailed,
			referenceId: missed.Id,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenMissedTransactionsExist_ShouldMarkMissed()
	{
		RecurringTransactionReadModel missed = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupMissedTransactions(transactions: [missed]);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkMissedAsync(
			recurringTransactionId: missed.Id,
			missedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: missed.RowVersion,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenMissedTransactionsExist_ShouldNotPublishForThem()
	{
		RecurringTransactionReadModel missed = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupMissedTransactions(transactions: [missed]);

		await _job.Execute(context: _jobContext);

		await _publisher.DidNotReceive().PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenEscalatingOneFails_ShouldStillEscalateTheOther()
	{
		RecurringTransactionReadModel first = RecurringTransactionFactory.CreateReadModel();
		RecurringTransactionReadModel second = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupMissedTransactions(transactions: [first, second]);

		int callCount = 0;
		_unresolvableEventWriteRepository.CreateAsync(
			type: Arg.Any<UnresolvableEventType>(),
			referenceId: Arg.Any<Guid>(),
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			callCount++;
			if (callCount == 1)
				throw new InvalidOperationException(message: "DB unavailable");
			return Task.CompletedTask;
		});

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkMissedAsync(
			recurringTransactionId: second.Id,
			missedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
