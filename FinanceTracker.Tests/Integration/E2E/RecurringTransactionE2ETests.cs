using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Contracts.Messages.RecurringTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Utilities;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: RecurringTransactionHandlingJob → RabbitMQ → RecurringTransactionConsumer → transaction created.
/// </summary>
public sealed class RecurringTransactionE2ETests : E2EFixture
{
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

	private async Task BackdateCreatedAtAsync(Guid recurringTransactionId, DateTimeOffset createdAt)
	{
		await using FinanceTrackerContext ctx = CreateReadContext();
		await ctx.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(setPropertyCalls: builder => builder.SetProperty(
			propertyExpression: r => r.CreatedAt,
			valueExpression: createdAt
		));
	}

	[Test]
	public async Task RecurringTransaction_DueToday_ShouldCreateTransactionAndUpdateLastExecutedAt()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		int todayDay = DateTime.UtcNow.Day;

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			dayOfMonth: todayDay
		);

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId && t.Amount == 1_500m);

		DateTimeOffset? lastExecutedAt = await readCtx.RecurringTransactions.Where(predicate: r => r.Id == recurringId)
			.Select(selector: r => r.LastExecutedAt)
			.FirstAsync();

		await Assert.That(value: transactionCreated).IsTrue();
		await Assert.That(value: lastExecutedAt).IsNotNull();
	}

	[Test]
	public async Task RecurringTransaction_AlreadyExecutedThisMonth_ShouldNotDuplicate()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		int todayDay = DateTime.UtcNow.Day;

		_ = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 2_000m,
			dayOfMonth: todayDay,
			lastExecutedAt: DateTimeOffset.UtcNow.AddHours(hours: -1) // already completed today
		);

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int txCount = await readCtx.Transactions.CountAsync(predicate: t => t.AccountId == accountId);

		await Assert.That(value: txCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task RecurringTransactionConsumer_DuplicateMessage_ShouldNotCreateTransactionTwice()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset now = DateTimeOffset.UtcNow;
		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 3_000m,
			dayOfMonth: now.Day
		);

		// Deterministic messageId is the same as that of a job
		Guid messageId = DeterministicGuid.Create(baseId: recurringId, year: now.Year, month: now.Month);

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
			OccurredAt: now,
			CorrelationId: Guid.NewGuid()
		);

		// Processing the same message twice directly
		await ProcessRecurringTransactionDirectAsync(message: message);
		await ProcessRecurringTransactionDirectAsync(message: message);

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int txCount = await readCtx.Transactions.CountAsync(predicate: t => t.AccountId == accountId && t.Amount == 3_000m);

		await Assert.That(value: txCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task RecurringTransaction_ScheduledDayAlreadyPassed_ShouldEscalateAndMarkMissed()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset now = DateTimeOffset.UtcNow;
		int scheduledDay = now.Day > 1 ? now.Day - 1 : 28;

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			dayOfMonth: scheduledDay
		);

		await BackdateCreatedAtAsync(recurringTransactionId: recurringId, createdAt: now.AddMonths(months: -2));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);

		DateTimeOffset? lastMissedAt = await readCtx.RecurringTransactions.Where(predicate: r => r.Id == recurringId)
			.Select(selector: r => r.LastMissedAt)
			.FirstAsync();

		await Assert.That(value: transactionCreated).IsFalse();
		await Assert.That(value: lastMissedAt).IsNotNull();
	}

	[Test]
	public async Task RecurringTransaction_ScheduledDayAlreadyPassed_WhenJobRunsAgain_ShouldNotEscalateTwice()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset now = DateTimeOffset.UtcNow;
		int scheduledDay = now.Day > 1 ? now.Day - 1 : 28;

		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			dayOfMonth: scheduledDay
		);

		await BackdateCreatedAtAsync(recurringTransactionId: recurringId, createdAt: now.AddMonths(months: -2));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);
		});

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();

		int escalationCount = await readCtx.UnresolvableEvents.CountAsync(predicate: e => e.ReferenceId == recurringId);
		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);

		await Assert.That(value: escalationCount).IsEqualTo(expected: 1);
		await Assert.That(value: transactionCreated).IsFalse();
	}

	[Test]
	public async Task RecurringTransaction_MissedAtMonthBoundary_ShouldEscalateRegardlessOfTodaysDay()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset now = DateTimeOffset.UtcNow;
		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			dayOfMonth: now.Day == 28 ? 27 : 28
		);

		await BackdateCreatedAtAsync(recurringTransactionId: recurringId, createdAt: now.AddMonths(months: -3));

		await RunRecurringTransactionJobAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool transactionCreated = await readCtx.Transactions.AnyAsync(predicate: t => t.AccountId == accountId);

		await Assert.That(value: transactionCreated).IsFalse();
	}

	[Test]
	public async Task RecurringTransaction_CreatedRecently_ShouldNotBeFalselyEscalatedAsCarriedOverMiss()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountViaCommandAsync(userId: userId, currencyCode: "RUB", balance: 50_000m);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset now = DateTimeOffset.UtcNow;
		int laterThisMonth = DateTime.DaysInMonth(year: now.Year, month: now.Month);
		Guid recurringId = await _recurringBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			amount: 1_500m,
			dayOfMonth: laterThisMonth == now.Day ? 1 : laterThisMonth // anything that isn't due today
		);

		await RunRecurringTransactionJobAsync();

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool escalated = await readCtx.UnresolvableEvents.AnyAsync(predicate: e => e.ReferenceId == recurringId);

		await Assert.That(value: escalated).IsFalse();
	}
}
