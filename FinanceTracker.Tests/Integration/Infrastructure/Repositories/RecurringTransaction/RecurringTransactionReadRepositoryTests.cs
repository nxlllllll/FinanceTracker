using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.RecurringTransaction;

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

		RecurringTransactionReadModel? result = await _readRepository.GetByIdAsync(
			recurringTransactionId: id,
			userId: userId
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: id);
		await Assert.That(value: result.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: result.RowVersion).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
	{
		RecurringTransactionReadModel? result = await _readRepository.GetByIdAsync(
			recurringTransactionId: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7()
		);

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

		PagedResult<RecurringTransactionReadModel> result = await _readRepository.GetByUserIdAsync(userId: userId);

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

		PagedResult<RecurringTransactionReadModel> result = await _readRepository.GetByUserIdAsync(userId: userId);

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

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		await _writeRepository.MarkExecutedAsync(
			recurringTransactionId: id,
			executedAt: DateTimeOffset.UtcNow,
			expectedVersion: 0
		);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		await _writeRepository.DeactivateAsync(recurringTransactionId: id, expectedVersion: 0);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueTodayAsync(
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

		PagedResult<RecurringTransactionReadModel> result = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);

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

		PagedResult<RecurringTransactionReadModel> firstPage = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);
		RecurringTransactionReadModel lastItem = firstPage.Items[^1];

		PagedResult<RecurringTransactionReadModel> secondPage = await _readRepository.GetByUserIdAsync(
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

		PagedResult<RecurringTransactionReadModel> firstPage = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 3);
		RecurringTransactionReadModel lastItem = firstPage.Items[^1];

		PagedResult<RecurringTransactionReadModel> secondPage = await _readRepository.GetByUserIdAsync(
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

		PagedResult<RecurringTransactionReadModel> result = await _readRepository.GetByUserIdAsync(userId: userId, pageSize: 10);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenScheduledDayHasPassed_ShouldReturnTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day == 1 ? 2 : now.Day;
		int scheduledDay = today - 1;

		// A pre-existing transaction (created well before its scheduled day this month) — a genuine
		// miss, as opposed to GetMissedThisMonthAsync_WhenCreatedThisMonthAfterScheduledDayPassed_ShouldNotReturn
		// below, which covers a transaction created *after* its scheduled day already passed.
		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: scheduledDay);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddMonths(months: -2));

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenCreatedThisMonthAfterScheduledDayPassed_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day == 1 ? 2 : now.Day;
		int scheduledDay = today - 1;

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: scheduledDay);
		await BackdateCreatedAtAsync(id: id, createdAt: currentMonthStart.AddDays(days: scheduledDay).AddHours(hours: 1));

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenScheduledForToday_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day;

		await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: today);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenAlreadyExecutedThisMonth_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day == 1 ? 2 : now.Day;
		int scheduledDay = today - 1;

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			dayOfMonth: scheduledDay, lastExecutedAt: now
		);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddMonths(months: -2));

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenAlreadyMarkedMissedThisMonth_ShouldNotReturnAgain()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day == 1 ? 2 : now.Day;
		int scheduledDay = today - 1;

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			dayOfMonth: scheduledDay, lastMissedAt: now
		);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddMonths(months: -2));

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenInactive_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		int today = now.Day == 1 ? 2 : now.Day;
		int scheduledDay = today - 1;

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: scheduledDay);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddMonths(months: -2));
		await _writeRepository.DeactivateAsync(recurringTransactionId: id, expectedVersion: 0);

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: today,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}
	
	[Test]
	public async Task GetMissedThisMonthAsync_WhenMissedAtMonthBoundary_ShouldReturnRegardlessOfTodaysDay()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 28);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddMonths(months: -3));

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		// Use day 1 specifically — the worst case for the old, broken comparison.
		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: 1,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetMissedThisMonthAsync_WhenCreatedDuringPreviousMonth_ShouldNotFalselyFlagBrandNewTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		Guid id = await _recurringTransactionBuilder.CreateAsync(userId: userId, accountId: accountId, categoryId: categoryId, dayOfMonth: 28);
		await BackdateCreatedAtAsync(id: id, createdAt: now.AddDays(days: -2));

		DateTimeOffset currentMonthStart = new DateTimeOffset(year: now.Year, month: now.Month, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero);
		DateTimeOffset previousMonthStart = currentMonthStart.AddMonths(months: -1);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetMissedThisMonthAsync(
			dayOfMonth: 1,
			currentMonthStart: currentMonthStart,
			previousMonthStart: previousMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}

	private async Task BackdateCreatedAtAsync(Guid id, DateTimeOffset createdAt)
	{
		await Context.RecurringTransactions.Where(predicate: r => r.Id == id).ExecuteUpdateAsync(setPropertyCalls: builder => builder.SetProperty(
			propertyExpression: r => r.CreatedAt,
			valueExpression: createdAt
		));
	}
}