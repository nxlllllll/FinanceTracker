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
		Core.Domains.RecurringTransaction.RecurringTransaction? result = await _readRepository.GetByIdAsync(recurringTransactionId: Guid.NewGuid());

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldReturnAllUserTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1
		);

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 15
		);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetByUserIdAsync_ShouldNotReturnOtherUserTransactions()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid anotherUserId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: anotherUserId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: anotherUserId);

		await _recurringTransactionBuilder.CreateAsync(
			userId: anotherUserId,
			accountId: accountId,
			categoryId: categoryId
		);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetByUserIdAsync(userId: userId);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetDueTodayAsync_WhenNeverExecuted_ShouldReturnTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTime now = DateTime.UtcNow;
		int today = now.Day;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: today
		);

		DateTime currentMonthStart = new DateTime(
			year: now.Year,
			month: now.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);

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
		DateTime now = DateTime.UtcNow;
		int today = now.Day;
		
		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: today
		);

		await _writeRepository.MarkExecutedAsync(recurringTransactionId: id, executedAt: DateTime.UtcNow);

		DateTime currentMonthStart = new DateTime(
			year: DateTime.UtcNow.Year,
			month: DateTime.UtcNow.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);

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
		DateTime now = DateTime.UtcNow;
		int today = now.Day;
		
		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: today
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id);

		DateTime currentMonthStart = new DateTime(
			year: DateTime.UtcNow.Year,
			month: DateTime.UtcNow.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);

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
		DateTime now = DateTime.UtcNow;
		int today = now.Day;
		int otherDay = today == 1 ? 2 : 1;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: otherDay
		);

		DateTime currentMonthStart = new DateTime(
			year: DateTime.UtcNow.Year,
			month: DateTime.UtcNow.Month,
			day: 1,
			hour: 0,
			minute: 0,
			second: 0,
			kind: DateTimeKind.Utc
		);

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
		int lastDay = daysInMonth;                                         
		int configuredDay = 31;                                            

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: configuredDay
		);

		DateTime currentMonthStart = new DateTime(
			year: year, month: month, day: 1,
			hour: 0, minute: 0, second: 0,
			kind: DateTimeKind.Utc
		);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: lastDay,
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
		int configuredDay = 31;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: configuredDay
		);

		DateTime currentMonthStart = new DateTime(
			year: year, month: month, day: 1,
			hour: 0, minute: 0, second: 0,
			kind: DateTimeKind.Utc
		);

		IReadOnlyList<Core.Domains.RecurringTransaction.RecurringTransaction> result = await _readRepository.GetDueTodayAsync(
			dayOfMonth: 15,
			daysInCurrentMonth: daysInMonth,
			currentMonthStart: currentMonthStart
		);

		await Assert.That(value: result).IsEmpty();
	}
}