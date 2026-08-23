using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.Utilities;
using FinanceTracker.Core.ValueObjects;
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

		SetupNoOverdueTransactions();

		_job = CreateJobAt(instant: FakeDateProvider.Default.UtcNow);
	}

	private void SetupNoOverdueTransactions()
	{
		_recurringTransactionReadRepository.GetOverdueAsync(
			before: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);
	}

	private void SetupEmptyRepository()
	{
		_recurringTransactionReadRepository.GetDueAsync(
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);
	}

	private void SetupRepository(int count)
	{
		IReadOnlyList<RecurringTransactionReadModel> transactions = [
			..Enumerable.Range(start: 0, count: count).Select(selector: _ => RecurringTransactionFactory.CreateReadModel())
		];

		SetupRepository(transactions: transactions);
	}

	private void SetupRepository(IReadOnlyList<RecurringTransactionReadModel> transactions)
	{
		_recurringTransactionReadRepository.GetDueAsync(
			asOf: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transactions);
	}

	private void SetupOverdueTransactions(IReadOnlyList<RecurringTransactionReadModel> transactions)
	{
		_recurringTransactionReadRepository.GetOverdueAsync(
			before: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transactions);
	}

	private RecurringTransactionHandlingJob CreateJobAt(DateTimeOffset instant)
	{
		return new RecurringTransactionHandlingJob(
			recurringTransactionReadRepository: _recurringTransactionReadRepository,
			recurringTransactionWriteRepository: _recurringTransactionWriteRepository,
			unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
			unitOfWork: _unitOfWork,
			correlationContext: _correlationContext,
			publisher: _publisher,
			dateProvider: new FakeDateProvider(utcNow: instant),
			options: new FakeOptionsMonitor<RecurringTransactionJobOptions>(value: new RecurringTransactionJobOptions()),
			logger: Substitute.For<ILogger<RecurringTransactionHandlingJob>>()
		);
	}

	private DateTimeOffset CapturedDueBound()
	{
		return (DateTimeOffset)_recurringTransactionReadRepository.ReceivedCalls()
			.Single(predicate: call => call.GetMethodInfo().Name == nameof(IRecurringTransactionReadRepository.GetDueAsync))
			.GetArguments()[0]!;
	}

	private DateTimeOffset CapturedOverdueBound()
	{
		return (DateTimeOffset)_recurringTransactionReadRepository.ReceivedCalls()
			.Single(predicate: call => call.GetMethodInfo().Name == nameof(IRecurringTransactionReadRepository.GetOverdueAsync))
			.GetArguments()[0]!;
	}

	[Test]
	public async Task Execute_WhenNoDueTransactions_ShouldNotPublish()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		await _publisher.DidNotReceive().PublishAsync(
			message: Arg.Any<RecurringTransactionTriggeredMessage>(),
			correlationId: Arg.Any<Guid>(),
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
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
	public async Task Execute_ShouldPublishWithTheInstantTheOperationWasDue()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		await _job.Execute(context: _jobContext);

		await _publisher.Received(requiredNumberOfCalls: 1).PublishAsync(
			message: Arg.Is<RecurringTransactionTriggeredMessage>(predicate: m => m!.OccurredAt == transaction.NextDueAtUtc),
			correlationId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenDueTransactionsExist_ShouldPublishWithDeterministicMessageId()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		Guid expectedMessageId = DeterministicGuid.Create(baseId: transaction.Id, occurrence: transaction.NextDueAtUtc);

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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldAdvanceTheScheduleWhenMarkingExecuted()
	{
		RecurringTransactionReadModel transaction = RecurringTransactionFactory.CreateReadModel();
		SetupRepository(transactions: [transaction]);

		DateTimeOffset expectedNext = RecurringDueDate.Next(
			dayOfMonth: transaction.DayOfMonth,
			timeZone: transaction.TimeZone,
			after: transaction.NextDueAtUtc
		);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: transaction.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			nextDueAtUtc: expectedNext,
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
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
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recurringTransactionWriteRepository.DidNotReceive().MarkExecutedAsync(
			recurringTransactionId: second.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkExecutedAsync(
			recurringTransactionId: third.Id,
			executedAt: Arg.Any<DateTimeOffset>(),
			nextDueAtUtc: Arg.Any<DateTimeOffset>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldAskWhatIsDueAsOfNow()
	{
		SetupEmptyRepository();

		await _job.Execute(context: _jobContext);

		await Assert.That(value: CapturedDueBound()).IsEqualTo(expected: FakeDateProvider.Default.UtcNow);
	}

	[Test]
	public async Task Execute_ShouldLookForOverdueOperationsBehindTheConfiguredThreshold()
	{
		SetupEmptyRepository();

		RecurringTransactionJobOptions options = new RecurringTransactionJobOptions();

		await _job.Execute(context: _jobContext);

		await Assert.That(value: CapturedOverdueBound())
			.IsEqualTo(expected: FakeDateProvider.Default.UtcNow.AddHours(hours: -options.OverdueAfterHours));
	}

	[Test]
	public async Task Execute_WhenNoOverdueTransactions_ShouldNotEscalate()
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
	public async Task Execute_WhenOverdueTransactionsExist_ShouldEscalateEach()
	{
		SetupEmptyRepository();
		SetupOverdueTransactions(transactions: [
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
	public async Task Execute_WhenOverdueTransactionsExist_ShouldEscalateWithCorrectReferenceId()
	{
		RecurringTransactionReadModel overdue = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupOverdueTransactions(transactions: [overdue]);

		await _job.Execute(context: _jobContext);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.RecurringTransactionFailed,
			referenceId: overdue.Id,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenOverdueTransactionsExist_ShouldMarkMissed()
	{
		RecurringTransactionReadModel overdue = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupOverdueTransactions(transactions: [overdue]);

		await _job.Execute(context: _jobContext);

		await _recurringTransactionWriteRepository.Received(requiredNumberOfCalls: 1).MarkMissedAsync(
			recurringTransactionId: overdue.Id,
			missedAt: Arg.Any<DateTimeOffset>(),
			expectedVersion: overdue.RowVersion,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenOverdueTransactionsExist_ShouldNotPublishForThem()
	{
		RecurringTransactionReadModel overdue = RecurringTransactionFactory.CreateReadModel();
		SetupEmptyRepository();
		SetupOverdueTransactions(transactions: [overdue]);

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
		SetupOverdueTransactions(transactions: [first, second]);

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

	[Test]
	public async Task DueOperations_ForAUserBehindUtc_ShouldNotFireBeforeTheirLocalDayBegins()
	{
		// 02:00 UTC on 1 September — already the 1st in UTC, still 31 August at UTC-10.
		DateTimeOffset instant = new DateTimeOffset(year: 2026, month: 9, day: 1, hour: 2, minute: 0, second: 0, offset: TimeSpan.Zero);

		// Mid-August, so the next occurrence of the 1st is September's rather than August's.
		DateTimeOffset reference = new DateTimeOffset(year: 2026, month: 8, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		RecurringTransactionHandlingJob job = CreateJobAt(instant: instant);

		SetupEmptyRepository();

		await job.Execute(context: _jobContext);

		DateTimeOffset bound = CapturedDueBound();

		DateTimeOffset honoluluDue = RecurringDueDate.Next(
			dayOfMonth: 1,
			timeZone: TimeZoneId.Create(value: "Pacific/Honolulu").Value,
			after: reference
		);

		await Assert.That(value: bound).IsEqualTo(expected: instant);

		await Assert.That(value: honoluluDue > bound).IsTrue().Because(message: $"""
			The operation is due at {honoluluDue:u}, which is midnight on 1 September in Honolulu. At
			{bound:u} that has not arrived, so the query does not return it.

			This is the inverse of what this test asserted before: the same instant used to produce
			day-of-month 1 and charge the user on their 31 August.
		""");
	}

	[Test]
	public async Task DueOperations_ForAUserAheadOfUtc_ShouldFireWhileUtcIsStillInThePreviousMonth()
	{
		// 12:00 UTC on 31 August — already 1 September at UTC+12.
		DateTimeOffset instant = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		DateTimeOffset reference = new DateTimeOffset(year: 2026, month: 8, day: 15, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		RecurringTransactionHandlingJob job = CreateJobAt(instant: instant);

		SetupEmptyRepository();

		await job.Execute(context: _jobContext);

		DateTimeOffset aucklandDue = RecurringDueDate.Next(
			dayOfMonth: 1,
			timeZone: TimeZoneId.Create(value: "Pacific/Auckland").Value,
			after: reference
		);

		await Assert.That(value: aucklandDue <= CapturedDueBound()).IsTrue().Because(message: $"""
			The operation is due at {aucklandDue:u} — midnight on 1 September in Auckland — and the job is
			asking what is due as of {instant:u}, so it is picked up.

			The due instant legitimately sits in the previous UTC month. That is the case the old
			day-of-month comparison could not express, and why the operation used to fire a day late for
			everyone east of UTC.
		""");
	}
}
