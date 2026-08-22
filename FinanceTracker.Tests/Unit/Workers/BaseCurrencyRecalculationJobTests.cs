using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Currency;
using FinanceTracker.Core.ReadModels.User;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.BaseCurrencyRecalculation.Job;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class BaseCurrencyRecalculationJobTests
{
	private static readonly Currency Usd = Currency.Reconstitute(value: "USD");
	private static readonly Currency Eur = Currency.Reconstitute(value: "EUR");

	private IBaseCurrencyRecalculationWriteRepository _recalculationWriteRepository = null!;
	private ICategoryTotalWriteRepository _categoryTotalWriteRepository = null!;
	private IUserQueryRepository _userQueryRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private BaseCurrencyRecalculationJobOptions _options = null!;
	private BaseCurrencyRecalculationJob _job = null!;

	private readonly Guid _userId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_recalculationWriteRepository = Substitute.For<IBaseCurrencyRecalculationWriteRepository>();
		_recalculationWriteRepository.CompleteAsync(
			userId: Arg.Any<Guid>(),
			targetCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		_categoryTotalWriteRepository = Substitute.For<ICategoryTotalWriteRepository>();
		_userQueryRepository = Substitute.For<IUserQueryRepository>();

		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_options = new BaseCurrencyRecalculationJobOptions { MaxAttempts = 3 };

		_job = new BaseCurrencyRecalculationJob(
			recalculationWriteRepository: _recalculationWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			userQueryRepository: _userQueryRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<BaseCurrencyRecalculationJobOptions>(value: _options),
			logger: NullLogger<BaseCurrencyRecalculationJob>.Instance
		);
	}

	private void Claims(params BaseCurrencyRecalculation[] rows) => _recalculationWriteRepository.ClaimPendingBatchAsync(
		batchSize: Arg.Any<int>(),
		leaseDuration: Arg.Any<TimeSpan>(),
		now: Arg.Any<DateTimeOffset>(),
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: rows.ToList());

	private BaseCurrencyRecalculation Request(
		Currency target,
		int attempts = 0
	) => new BaseCurrencyRecalculation(
		UserId: _userId,
		Status: BaseCurrencyRecalculationStatus.InProgress,
		TargetCurrency: target,
		RequestedAt: FakeDateProvider.Default.UtcNow,
		Attempts: attempts,
		LastError: null
	);

	private void UserHasCurrency(Currency currency) => _userQueryRepository.GetByIdAsync(
		userId: _userId,
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: new UserReadModel(
		Id: _userId,
		Email: Email.Reconstitute(value: "someone@example.com"),
		BaseCurrency: currency,
		CreatedAt: FakeDateProvider.Default.UtcNow
	));

	private static IJobExecutionContext ContextWith(CancellationToken ct)
	{
		IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
		context.CancellationToken.Returns(returnThis: ct);
		return context;
	}

	[Test]
	public async Task Execute_ShouldRebuildAndMarkDone()
	{
		Claims(Request(target: Usd));
		UserHasCurrency(currency: Usd);

		await _job.Execute(context: ContextWith(ct: CancellationToken.None));

		await _categoryTotalWriteRepository.Received(requiredNumberOfCalls: 1).RecalculateAllForUserAsync(
			userId: _userId,
			baseCurrency: Usd,
			ct: Arg.Any<CancellationToken>()
		);

		await _recalculationWriteRepository.Received(requiredNumberOfCalls: 1).CompleteAsync(
			userId: _userId,
			targetCurrency: Usd,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTheUserMovedOnSinceTheClaim_ShouldNotRebuildTowardsTheOldTarget()
	{
		Claims(Request(target: Usd));
		UserHasCurrency(currency: Eur);

		await _job.Execute(context: ContextWith(ct: CancellationToken.None));

		await _categoryTotalWriteRepository.DidNotReceive().RecalculateAllForUserAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recalculationWriteRepository.DidNotReceive().CompleteAsync(
			userId: Arg.Any<Guid>(),
			targetCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _recalculationWriteRepository.DidNotReceive().FailAttemptAsync(
			userId: Arg.Any<Guid>(),
			error: Arg.Any<string>(),
			maxAttempts: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTheCurrencyChangesMidRebuild_ShouldNotCountItAsAFailure()
	{
		Claims(Request(target: Usd));
		UserHasCurrency(currency: Usd);

		_recalculationWriteRepository.CompleteAsync(
			userId: Arg.Any<Guid>(),
			targetCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		await _job.Execute(context: ContextWith(ct: CancellationToken.None));

		await _recalculationWriteRepository.DidNotReceive().FailAttemptAsync(
			userId: Arg.Any<Guid>(),
			error: Arg.Any<string>(),
			maxAttempts: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTheRebuildThrows_ShouldRecordTheAttemptWithTheReason()
	{
		Claims(Request(target: Usd, attempts: 1));
		UserHasCurrency(currency: Usd);

		_categoryTotalWriteRepository.RecalculateAllForUserAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new InvalidOperationException(message: "rate lookup failed"));

		await _job.Execute(context: ContextWith(ct: CancellationToken.None));

		await _recalculationWriteRepository.Received(requiredNumberOfCalls: 1).FailAttemptAsync(
			userId: _userId,
			error: "rate lookup failed",
			maxAttempts: _options.MaxAttempts,
			ct: Arg.Any<CancellationToken>()
		);

		await _recalculationWriteRepository.DidNotReceive().CompleteAsync(
			userId: Arg.Any<Guid>(),
			targetCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenOneUserFails_ShouldStillProcessTheRest()
	{
		Guid secondUserId = Guid.CreateVersion7();

		Claims(
			Request(target: Usd),
			new BaseCurrencyRecalculation(
				UserId: secondUserId,
				Status: BaseCurrencyRecalculationStatus.InProgress,
				TargetCurrency: Usd,
				RequestedAt: FakeDateProvider.Default.UtcNow,
				Attempts: 0,
				LastError: null
			)
		);

		UserHasCurrency(currency: Usd);
		_userQueryRepository.GetByIdAsync(userId: secondUserId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: new UserReadModel(
			Id: secondUserId,
			Email: Email.Reconstitute(value: "other@example.com"),
			BaseCurrency: Usd,
			CreatedAt: FakeDateProvider.Default.UtcNow
		));

		_categoryTotalWriteRepository.RecalculateAllForUserAsync(
			userId: _userId,
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).ThrowsAsync(ex: new InvalidOperationException(message: "boom"));

		await _job.Execute(context: ContextWith(ct: CancellationToken.None));

		await _recalculationWriteRepository.Received(requiredNumberOfCalls: 1).CompleteAsync(
			userId: secondUserId,
			targetCurrency: Usd,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenCancelledDuringARebuild_ShouldStillRecordTheAttempt()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();

		Claims(Request(target: Usd));
		UserHasCurrency(currency: Usd);

		_categoryTotalWriteRepository.RecalculateAllForUserAsync(
			userId: Arg.Any<Guid>(),
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			cts.Cancel();
			throw new InvalidOperationException(message: "interrupted");
		});

		await _job.Execute(context: ContextWith(ct: cts.Token));

		await _recalculationWriteRepository.Received(requiredNumberOfCalls: 1).FailAttemptAsync(
			userId: _userId,
			error: Arg.Any<string>(),
			maxAttempts: Arg.Any<int>(),
			ct: CancellationToken.None
		);
	}

	[Test]
	public async Task Execute_WhenCancelledBetweenUsers_ShouldStopRatherThanFinishTheBatch()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();
		Guid secondUserId = Guid.CreateVersion7();

		Claims(
			Request(target: Usd),
			new BaseCurrencyRecalculation(
				UserId: secondUserId,
				Status: BaseCurrencyRecalculationStatus.InProgress,
				TargetCurrency: Usd,
				RequestedAt: FakeDateProvider.Default.UtcNow,
				Attempts: 0,
				LastError: null
			)
		);

		UserHasCurrency(currency: Usd);
		_userQueryRepository.GetByIdAsync(userId: secondUserId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: new UserReadModel(
			Id: secondUserId,
			Email: Email.Reconstitute(value: "other@example.com"),
			BaseCurrency: Usd,
			CreatedAt: FakeDateProvider.Default.UtcNow
		));

		_categoryTotalWriteRepository.RecalculateAllForUserAsync(
			userId: _userId,
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			cts.Cancel();
			return Task.CompletedTask;
		});

		await _job.Execute(context: ContextWith(ct: cts.Token));

		await _categoryTotalWriteRepository.DidNotReceive().RecalculateAllForUserAsync(
			userId: secondUserId,
			baseCurrency: Arg.Any<Currency>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenDisabled_ShouldNotClaimAnything()
	{
		BaseCurrencyRecalculationJobOptions options = new BaseCurrencyRecalculationJobOptions { IsEnabled = false };

		BaseCurrencyRecalculationJob disabled = new BaseCurrencyRecalculationJob(
			recalculationWriteRepository: _recalculationWriteRepository,
			categoryTotalWriteRepository: _categoryTotalWriteRepository,
			userQueryRepository: _userQueryRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<BaseCurrencyRecalculationJobOptions>(value: options),
			logger: NullLogger<BaseCurrencyRecalculationJob>.Instance
		);

		await disabled.Execute(context: ContextWith(ct: CancellationToken.None));

		await _recalculationWriteRepository.DidNotReceive().ClaimPendingBatchAsync(
			batchSize: Arg.Any<int>(),
			leaseDuration: Arg.Any<TimeSpan>(),
			now: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
