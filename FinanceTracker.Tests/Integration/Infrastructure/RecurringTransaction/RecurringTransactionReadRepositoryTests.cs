using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.RecurringTransaction;

public sealed class RecurringTransactionReadRepositoryTests : DatabaseFixture
{
	private RecurringTransactionReadRepository _readRepository = null!;
	private RecurringTransactionWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private RecurringTransactionBuilder _recurringTransactionBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new RecurringTransactionReadRepository(context: Context);
		_writeRepository = new RecurringTransactionWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_recurringTransactionBuilder = new RecurringTransactionBuilder(context: Context);
	}

	[Test]
	public async Task GetByIdAsync_WhenExists_ShouldReturnDto()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		Core.Domains.RecurringTransaction.RecurringTransaction? result = await _readRepository.GetByIdAsync(recurringTransactionId: id);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: id);
		await Assert.That(value: result.UserId).IsEqualTo(expected: userId);
	}

	[Test]
	public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
	{
		Core.Domains.RecurringTransaction.RecurringTransaction? result = await _readRepository.GetByIdAsync(recurringTransactionId: Guid.CreateVersion7());

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldReturnAllUserTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 1);
		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 15);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldNotReturnOtherUserTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid anotherUserId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: anotherUserId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: anotherUserId);

		await _recurringTransactionBuilder.CreateAsync(userId: anotherUserId, accountId: accountId, categoryId: categoryId);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetDueTodayAsync_WhenNeverExecuted_ShouldReturnTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day;

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: today);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: today,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetDueTodayAsync_WhenAlreadyExecutedThisMonth_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day;

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: today);

		await _writeRepository.MarkExecutedAsync(recurringTransactionId: id, executedAt: DateTimeOffset.UtcNow);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: today,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetDueTodayAsync_WhenInactive_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day;

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: today);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: today,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetDueTodayAsync_WhenDifferentDay_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day;
		int otherDay = today == 1 ? 2 : 1;

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: otherDay);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: today,
			daysInCurrentMonth: DateTime.DaysInMonth(year: now.Year, month: now.Month),
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetDueTodayAsync_WhenDayOfMonthExceedsMonthLength_AndTodayIsLastDay_ShouldReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		int year = 2024;
		int month = 2;
		int daysInMonth = DateTime.DaysInMonth(year: year, month: month);

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 31);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: year, month: month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: daysInMonth,
			daysInCurrentMonth: daysInMonth,
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetDueTodayAsync_WhenDayOfMonthExceedsMonthLength_AndTodayIsNotLastDay_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		int year = 2024;
		int month = 2;
		int daysInMonth = DateTime.DaysInMonth(year: year, month: month);

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 31);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: year, month: month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: 15,
			daysInCurrentMonth: daysInMonth,
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetByUserIdAsync_WithoutCursor_ShouldReturnFirstPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		for (int i = 0; i < 5; i++)
			await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: i + 1);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 3);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorDate).IsNotNull();
		await Assert.That(value: result.NextCursorId).IsNotNull();
	}

	[Test]
	public async Task GetByUserIdAsync_WithCursor_ShouldReturnNextPage()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		for (int i = 0; i < 5; i++)
			await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: i + 1);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> firstPage = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);

		Core.Domains.RecurringTransaction.RecurringTransaction lastItem = firstPage.Items[^1];

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> secondPage = await _readRepository.GetByUserIdAsync(
			userId: userId,
			cursorCreatedAt: lastItem.CreatedAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
		await Assert.That(value: secondPage.Items.Any(r => firstPage.Items.Any(f => f.Id == r.Id))).IsFalse();
	}

	[Test]
	public async Task GetByUserIdAsync_WhenNoMoreItems_ShouldReturnEmptyList()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> firstPage = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);

		Core.Domains.RecurringTransaction.RecurringTransaction lastItem = firstPage.Items[^1];

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> secondPage = await _readRepository.GetByUserIdAsync(
			userId: userId,
			cursorCreatedAt: lastItem.CreatedAt,
			cursorId: lastItem.Id,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items).IsEmpty();
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldNotReturnOtherUserItems()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid otherUserId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: otherUserId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: otherUserId);

		for (int i = 0; i < 3; i++)
			await _recurringTransactionBuilder.CreateAsync(userId: otherUserId, accountId: accountId, categoryId: categoryId, dayOfMonth: i + 1);

		PagedResult<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 10);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
	}
}
