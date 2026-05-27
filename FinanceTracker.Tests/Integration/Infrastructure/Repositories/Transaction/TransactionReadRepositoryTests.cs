using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transaction;

public sealed class TransactionReadRepositoryTests : DatabaseFixture
{
	private TransactionReadRepository _readRepository = null!;
	private TransactionWriteRepository _writeRepository = null!;
	private AccountWriteRepository _accountWriteRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private TransactionBuilder _transactionBuilder = null!;
	private IUnitOfWork _unitOfWork = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new TransactionReadRepository(context: Context);
		_writeRepository = new TransactionWriteRepository(context: Context);
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
		_accountWriteRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_transactionBuilder = new TransactionBuilder(context: Context);
	}

	private async Task<(Guid accountId, Guid categoryId, Guid userId)> CreateAccountAndCategoryAsync()
	{
		string currencyCode = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

		Guid accountId = Guid.CreateVersion7();
		await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: userId,
			Name: Name.Create(value: "����� ����").Value,
			Type: AccountType.Checking,
			Currency: Core.ValueObjects.Currency.Create(value: currencyCode).Value,
			Balance: 10000m,
			OccurredAt: DateTimeOffset.UtcNow
		));

		Guid categoryId = Guid.CreateVersion7();
		await Context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = Name.Create(value: "���").Value,
			Type = CategoryType.Expense,
			IsArchived = false,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();

		return (accountId, categoryId, userId);
	}

	private async Task<Guid> CreateTransactionAsync(
		Guid accountId,
		Guid categoryId,
		Guid userId,
		DirectionType direction = DirectionType.Debit,
		bool isExcluded = false,
		DateTimeOffset? occurredAt = null)
	{
		Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(),
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: direction,
			exchangeRate: 1m,
			isExcluded: false,
			isRatePending: false,
			description: "����",
			occurredAt: occurredAt ?? DateTimeOffset.UtcNow
		);

		await _writeRepository.CreateAsync(transaction: transaction);

		if (isExcluded)
			await _writeRepository.ExcludeAsync(transactionId: transaction.Id);

		return transaction.Id;
	}

	[Test]
	public async Task GetByIdAsync_WithNonExistentTransaction_ShouldReturnNull()
	{
		Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(
			transactionId: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7()
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingTransaction_ShouldReturnCorrectDto()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		Guid transactionId = await CreateTransactionAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId
		);

		Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(
			transactionId: transactionId,
			userId: userId
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: transactionId);
		await Assert.That(value: result.AccountId).IsEqualTo(expected: accountId);
		await Assert.That(value: result.Amount.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: result.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: result.IsExcluded).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithNoTransactions_ShouldReturnEmptyList()
	{
		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            userId: Guid.CreateVersion7(),
			accountId: Guid.CreateVersion7()
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 0);
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnOnlyAccountTransactions()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
		(Guid anotherAccountId, Guid anotherCategoryId, Guid anotherUserId) = await CreateAccountAndCategoryAsync();

		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId);
		await CreateTransactionAsync(userId: anotherUserId, accountId: anotherAccountId, categoryId: anotherCategoryId);

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].AccountId).IsEqualTo(expected: accountId);
	}

	[Test]
	public async Task GetAllAsync_WithDirectionFilter_ShouldReturnOnlyMatchingTransactions()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, direction: DirectionType.Debit);
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, direction: DirectionType.Credit);

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			direction: DirectionType.Debit
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].Direction).IsEqualTo(expected: DirectionType.Debit);
	}

	[Test]
	public async Task GetAllAsync_WithIsExcludedFilter_ShouldReturnOnlyMatchingTransactions()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, isExcluded: false);
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, isExcluded: true);

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			isExcluded: false
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result.Items[0].IsExcluded).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithDateRangeFilter_ShouldReturnOnlyMatchingTransactions()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow.AddDays(days: -10));
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow.AddDays(days: -3));
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow);

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			dateFrom: DateTimeOffset.UtcNow.AddDays(days: -5),
			dateTo: DateTimeOffset.UtcNow.AddDays(days: 1)
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnTransactionsOrderedByDateDescending()
	{
		(Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow.AddDays(days: -2));
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow);
		await CreateTransactionAsync(userId: userId, accountId: accountId, categoryId: categoryId, occurredAt: DateTimeOffset.UtcNow.AddDays(days: -1));

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId
		);

		await Assert.That(value: result.Items[0].OccurredAt).IsGreaterThan(minimum: result.Items[1].OccurredAt);
		await Assert.That(value: result.Items[1].OccurredAt).IsGreaterThan(minimum: result.Items[2].OccurredAt);
	}

	[Test]
	public async Task GetAllAsync_WithoutCursor_ShouldReturnFirstPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		for (int i = 0; i < 5; i++)
			await _transactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			pageSize: 3
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 3);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorDate).IsNotNull();
		await Assert.That(value: result.NextCursorId).IsNotNull();
	}

	[Test]
	public async Task GetAllAsync_WithCursor_ShouldReturnNextPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset baseTime = new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		for (int i = 0; i < 5; i++)
		{
			await _transactionBuilder.CreateAsync(
				userId: userId,
				accountId: accountId,
				categoryId: categoryId,
				occurredAt: baseTime.AddHours(hours: i)
			);
		}

		PagedResult<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			pageSize: 3
		);

		Core.Domains.Transaction.Transaction lastItem = firstPage.Items[^1];

		PagedResult<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			cursorOccurredAt: lastItem.OccurredAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
		await Assert.That(value: secondPage.Items.Any(t => firstPage.Items.Any(f => f.Id == t.Id))).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_WithCursor_ShouldNotReturnDuplicates()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset baseTime = new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		for (int i = 0; i < 6; i++)
		{
			await _transactionBuilder.CreateAsync(
				userId: userId,
				accountId: accountId,
				categoryId: categoryId,
				occurredAt: baseTime.AddHours(hours: i)
			);
		}

		PagedResult<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			pageSize: 3
		);

		Core.Domains.Transaction.Transaction lastItem = firstPage.Items[^1];

		PagedResult<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			cursorOccurredAt: lastItem.OccurredAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		IEnumerable<Guid> allIds = firstPage.Items.Select(t => t.Id).Concat(secondPage.Items.Select(t => t.Id));
		await Assert.That(value: allIds.Distinct().Count()).IsEqualTo(expected: 6);
	}

	[Test]
	public async Task GetAllAsync_WhenNoMoreItems_ShouldReturnEmptyList()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _transactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			occurredAt: new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero)
		);

		PagedResult<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			pageSize: 3
		);

		Core.Domains.Transaction.Transaction lastItem = firstPage.Items[^1];

		PagedResult<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			cursorOccurredAt: lastItem.OccurredAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items).IsEmpty();
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnItemsOrderedByOccurredAtDescending()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset baseTime = new DateTimeOffset(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		for (int i = 0; i < 3; i++)
		{
			await _transactionBuilder.CreateAsync(
				userId: userId,
				accountId: accountId,
				categoryId: categoryId,
				occurredAt: baseTime.AddHours(hours: i)
			);
		}

		PagedResult<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
			userId: userId,
			accountId: accountId,
			pageSize: 10
		);

		await Assert.That(value: result.Items[0].OccurredAt).IsGreaterThan(minimum: result.Items[1].OccurredAt);
		await Assert.That(value: result.Items[1].OccurredAt).IsGreaterThan(minimum: result.Items[2].OccurredAt);
	}
}
