using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CreateAccount / RenameAccount / ArchiveAccount / CreateTransaction
/// → outbox → RabbitMQ → AccountEventsConsumer → AccountProjection → read model.
/// </summary>
public sealed class AccountProjectionE2ETests : E2EFixture
{
	private UserBuilder _userBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;

	[Before(hookType: Test)]
	public async Task SetupDataAsync()
	{
		_userBuilder = new UserBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
	}

	private async Task<Guid> CreateAccountAsync(Guid userId, decimal balance = 10_000m)
	{
		Result<Guid, AppException> result = await Mediator.Send(request: new CreateAccountCommand(
			UserId: userId,
			Name: Name.Create(value: "Тестовый счёт").Value,
			Type: AccountType.Checking,
			Currency: Currency.Create(value: "RUB").Value,
			InitialBalance: balance
		) { IdempotencyKey = Guid.CreateVersion7() });
		return result.Value!;
	}

	[Test]
	public async Task CreateAccount_AfterOutbox_ShouldProjectAccountAndBalance()
	{
		Guid userId = await _userBuilder.CreateAsync();

		Guid accountId = await CreateAccountAsync(userId: userId, balance: 5_000m);
		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();

		bool accountExists = await readCtx.Accounts.AnyAsync(predicate: a => a.Id == accountId && a.UserId == userId);

		decimal balance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: accountExists).IsTrue();
		await Assert.That(value: balance).IsEqualTo(expected: 5_000m);
	}

	[Test]
	public async Task CreateAccount_ThenDebitTransaction_ShouldReduceProjectedBalance()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 10_000m);

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: 3_000m,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: DirectionType.Debit,
			Description: null,
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			decimal b = await ctx.AccountBalances.Where(predicate: x => x.AccountId == accountId)
				.Select(selector: x => x.Balance)
				.FirstOrDefaultAsync();
			return b == 7_000m;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		decimal balance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 7_000m);
	}

	[Test]
	public async Task CreateAccount_ThenCreditTransaction_ShouldIncreaseProjectedBalance()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId, type: Core.Domains.Category.CategoryType.Income);
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 1_000m);

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		await Mediator.Send(request: new CreateTransactionCommand(
			AccountId: accountId,
			UserId: userId,
			CategoryId: categoryId,
			Amount: 5_000m,
			Currency: Currency.Create(value: "RUB").Value,
			Direction: DirectionType.Credit,
			Description: null,
			OccurredAt: DateTimeOffset.UtcNow
		)
		{ IdempotencyKey = Guid.CreateVersion7() });

		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			decimal b = await ctx.AccountBalances.Where(predicate: x => x.AccountId == accountId)
				.Select(selector: x => x.Balance)
				.FirstOrDefaultAsync();
			return b == 6_000m;
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		decimal balance = await readCtx.AccountBalances
			.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

		await Assert.That(value: balance).IsEqualTo(expected: 6_000m);
	}

	[Test]
	public async Task RenameAccount_AfterOutbox_ShouldUpdateProjectedName()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId);

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		await Mediator.Send(request: new RenameAccountCommand(
			UserId: userId,
			AccountId: accountId,
			NewName: Name.Create(value: "Переименованный счёт").Value
		));

		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			string? name = await ctx.Accounts.Where(predicate: a => a.Id == accountId)
				.Select(selector: a => a.Name.Value)
				.FirstOrDefaultAsync();
			return name == "Переименованный счёт";
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		string projectedName = await readCtx.Accounts.Where(predicate: a => a.Id == accountId)
			.Select(selector: a => a.Name.Value)
			.FirstAsync();

		await Assert.That(value: projectedName).IsEqualTo(expected: "Переименованный счёт");
	}

[Test]
	public async Task ArchiveAccount_AfterOutbox_ShouldProjectIsArchivedTrue()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await CreateAccountAsync(userId: userId, balance: 0m);

		await RunOutboxAsync();
		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
		});

		await Mediator.Send(request: new ArchiveAccountCommand(UserId: userId, AccountId: accountId));
		await RunOutboxAsync();

		await WaitForConditionAsync(condition: async () =>
		{
			await using FinanceTrackerContext ctx = CreateReadContext();
			return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId && a.IsArchived);
		});

		await using FinanceTrackerContext readCtx = CreateReadContext();
		bool isArchived = await readCtx.Accounts.Where(predicate: a => a.Id == accountId)
			.Select(selector: a => a.IsArchived)
			.FirstAsync();

		await Assert.That(value: isArchived).IsTrue();
	}
}
