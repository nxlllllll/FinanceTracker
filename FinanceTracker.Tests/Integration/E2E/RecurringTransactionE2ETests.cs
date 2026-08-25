using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Utilities;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: RecurringTransactionHandlingJob → RabbitMQ → RecurringTransactionConsumer → transaction created.
/// </summary>
public sealed class RecurringTransactionE2ETests : E2EFixture
{
	private sealed class MutableDateProvider(DateTimeOffset utcNow) : IDateProvider
	{
		public DateTimeOffset UtcNow { get; private set; } = utcNow;

		public DateOnly UtcToday { get; } = DateOnly.FromDateTime(dateTime: utcNow.UtcDateTime);

		public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(timeSpan: by);
	}

	private readonly MutableDateProvider _clock = new MutableDateProvider(utcNow: DateTimeOffset.UtcNow);

	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private RecurringTransactionBuilder _recurringBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_recurringBuilder = new RecurringTransactionBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
	}

	protected override void ConfigureAdditionalServices(
		IServiceCollection services,
		IConfiguration configuration
	) => services.AddSingleton<IDateProvider>(implementationInstance: _clock);

	private static RecurringTransactionTriggeredMessage BuildMessage(
		Guid recurringId,
		Guid accountId,
		Guid userId,
		Guid categoryId,
		decimal amount,
		DateTimeOffset occurrence
	) => new RecurringTransactionTriggeredMessage(
		MessageId: DeterministicGuid.Create(baseId: recurringId, occurrence: occurrence),
		RecurringTransactionId: recurringId,
		AccountId: accountId,
		UserId: userId,
		CategoryId: categoryId,
		Amount: amount,
		Currency: "RUB",
		Direction: "Debit",
		Description: null,
		OccurredAt: occurrence,
		CorrelationId: Guid.CreateVersion7()
	);

	private async Task<Guid> CreateAccountViaCommandAsync(Guid userId, string currencyCode, decimal balance)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: currencyCode).Value,
			InitialBalance: balance
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		Guid accountId = result.Value!;

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		return accountId;
	}

	[Test]
	public async Task RecurringTransaction_WhenDue_ShouldCreateTransactionAndUpdateLastExecutedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset dueAt = _clock.UtcNow.AddMinutes(minutes: 1);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			nextDueAtUtc: dueAt
		);

		_clock.Advance(by: TimeSpan.FromHours(hours: 2));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId && t.Amount == 1_500m);

		var schedule = await readCtx.RecurringTransactions.Where(predicate: r => r.Id == recurringId)
			.Select(selector: r => new { r.LastExecutedAt, r.NextDueAtUtc })
			.FirstAsync();

		await Assert.That(value: transactionCreated).IsTrue();
		await Assert.That(value: schedule.LastExecutedAt).IsNotNull();

		await Assert.That(value: schedule.NextDueAtUtc > dueAt).IsTrue().Because(message: $"""
			The schedule has to move past {dueAt:u}, or the operation is still due and the next run
			executes it a second time. Recording the execution without advancing the instant is the one
			failure this whole design has to rule out.
		""");
	}

	[Test]
	public async Task RecurringTransaction_WhenNotYetDue_ShouldNotFire()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		_ = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 2_000m,
			nextDueAtUtc: _clock.UtcNow.AddHours(hours: 5)
		);

		_clock.Advance(by: TimeSpan.FromHours(hours: 1));

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int txCount = await readCtx.Transactions.CountAsync(predicate: t => t.AccountId == accountId);

		await Assert.That(value: txCount).IsEqualTo(expected: 0).Because(message: """
			Replaces the old "already executed this month" test. Not firing early no longer follows from
			last_executed_at being set — the due instant simply has not arrived, which is the same
			mechanism that stops an operation running twice after it has.
		""");
	}

	[Test]
	public async Task RecurringTransactionConsumer_DuplicateMessage_ShouldNotCreateTransactionTwice()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset dueAt = _clock.UtcNow.AddMinutes(minutes: 1);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			nextDueAtUtc: dueAt
		);

		_clock.Advance(by: TimeSpan.FromHours(hours: 2));

		Guid messageId = DeterministicGuid.Create(baseId: recurringId, occurrence: dueAt);

		RecurringTransactionTriggeredMessage message = new RecurringTransactionTriggeredMessage(
			MessageId: messageId,
			RecurringTransactionId: recurringId,
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: 3_000m,
			Currency: "RUB",
			Direction: "Debit",
			Description: null,
			OccurredAt: dueAt,
			CorrelationId: Guid.CreateVersion7()
		);

		await ProcessRecurringTransactionDirectAsync(message: message);
		await ProcessRecurringTransactionDirectAsync(message: message);

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int txCount = await readCtx.Transactions.CountAsync(predicate: t => t.AccountId == accountId && t.Amount == 3_000m);

		await Assert.That(value: txCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecurringTransactionConsumer_TwoOccurrencesInOneMonth_ShouldCreateBothTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset firstOccurrence = _clock.UtcNow.AddMinutes(minutes: 1);
		DateTimeOffset secondOccurrence = _clock.UtcNow.AddDays(days: 5);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 4_000m,
			nextDueAtUtc: firstOccurrence
		);

		_clock.Advance(by: TimeSpan.FromDays(days: 6));

		await ProcessRecurringTransactionDirectAsync(message: BuildMessage(
			recurringId: recurringId,
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			amount: 4_000m,
			occurrence: firstOccurrence
		));

		await ProcessRecurringTransactionDirectAsync(message: BuildMessage(
			recurringId: recurringId,
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			amount: 4_000m,
			occurrence: secondOccurrence
		));

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int txCount = await readCtx.Transactions.CountAsync(predicate: t => t.AccountId == accountId && t.Amount == 4_000m);

		await Assert.That(value: txCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task RecurringTransaction_WhenOverdueBeyondTheThreshold_ShouldEscalateAndMarkMissed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 999_999m,
			nextDueAtUtc: _clock.UtcNow.AddMinutes(minutes: 1)
		);

		_clock.Advance(by: TimeSpan.FromDays(days: 3));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);

		await Assert.That(value: transactionCreated).IsFalse().Because(message: """
			Insufficient funds, so nothing was created and the operation is still owed. That is what
			"overdue" has to mean now: not that the schedule slipped — a stored instant cannot slip — but
			that something is past due and unresolved.
		""");
	}

	[Test]
	public async Task RecurringTransaction_WhenOverdue_AndJobRunsAgain_ShouldNotEscalateTwice()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 999_999m,
			nextDueAtUtc: _clock.UtcNow.AddMinutes(minutes: 1)
		);

		_clock.Advance(by: TimeSpan.FromDays(days: 3));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UnresolvableEvents.CountAsync(predicate: e => e.ReferenceId == recurringId) >= 2;
		});

		int firstRunCount;

		await using (FinanceTrackerContext afterFirstRun = CreateReadContext())
			firstRunCount = await afterFirstRun.UnresolvableEvents.CountAsync(predicate: e => e.ReferenceId == recurringId);

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int totalCount = await readCtx.UnresolvableEvents.CountAsync(predicate: e => e.ReferenceId == recurringId);

		await Assert.That(value: totalCount).IsEqualTo(expected: firstRunCount).Because(message: $"""
			The second run added {totalCount - firstRunCount} escalation(s) for an operation already
			marked. The mark is compared against the due instant, so it stays current while the schedule
			has not moved past it — one outage produces one alert, and next month's occurrence produces
			its own.
		""");
	}

	[Test]
	public async Task RecurringTransaction_WhenRecentlyDue_ShouldNotBeEscalatedBeforeItHasHadAChanceToRun()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			nextDueAtUtc: _clock.UtcNow.AddMinutes(minutes: 1)
		);

		_clock.Advance(by: TimeSpan.FromHours(hours: 2));

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool escalated = await readCtx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);

		await Assert.That(value: escalated).IsFalse().Because(message: """
			An operation that came due two hours ago is executed by this very run, well inside the
			threshold. Escalating it would raise an alert for every payment shortly after it fell due,
			which is the failure mode the threshold exists to prevent.
		""");
	}

	[Test]
	public async Task RecurringTransaction_WhenNotYetDue_ShouldNotBeEscalated()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			nextDueAtUtc: _clock.UtcNow.AddDays(days: 10)
		);

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool escalated = await readCtx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);

		await Assert.That(value: escalated).IsFalse().Because(message: """
			Replaces the test that guarded against a freshly created operation being reported as a
			carried-over miss. That was possible when the query inferred misses from day_of_month and
			created_at; a future instant cannot be overdue by construction, and this pins that.
		""");
	}
}
