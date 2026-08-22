using FinanceTracker.Core.ReadModels.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

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
	public async Task GetDueAsync_WhenTheDueInstantHasArrived_ShouldReturnTransaction()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddHours(hours: -1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueAsync(asOf: now);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetDueAsync_WhenTheDueInstantIsStillAhead_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddHours(hours: 1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueAsync(asOf: now);

		await Assert.That(value: result).IsEmpty().Because(message: """
			One hour is well inside a single poll interval. Firing early by any margin means charging
			someone before the day they chose has begun.
		""");
	}

	[Test]
	public async Task GetDueAsync_WhenInactive_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddHours(hours: -1)
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id, expectedVersion: 0);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueAsync(asOf: now);

		await Assert.That(value: result).IsEmpty();
	}

	[Test]
	public async Task GetDueAsync_ShouldReturnTheOwnersTimeZone()
	{
		TimeZoneId auckland = TimeZoneId.Create(value: "Pacific/Auckland").Value;

		Guid userId = await _userBuilder.CreateAsync(timeZone: auckland);
		Guid otherUserId = await _userBuilder.CreateAsync(timeZone: TimeZoneId.Create(value: "Pacific/Honolulu").Value);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddHours(hours: -1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetDueAsync(asOf: now);

		await Assert.That(value: result.Single().TimeZone).IsEqualTo(expected: auckland).Because(message: $"""
			The zone is joined from users, and the job recalculates the next occurrence with it. A join
			that picked up the wrong row would move someone's schedule by hours without any error — the
			second user here exists to make that failure possible rather than theoretical.
		""");
	}

	[Test]
	public async Task GetOverdueAsync_WhenDueLongerAgoThanTheThreshold_ShouldReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddDays(days: -2)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetOverdueAsync(before: now.AddDays(days: -1));

		await Assert.That(value: result.Count).IsEqualTo(expected: 1).Because(message: """
			Due two days ago and still unexecuted is not polling lag — nothing consumed it, and without
			this query nobody would notice until the user asked where their payment went.
		""");
	}

	[Test]
	public async Task GetOverdueAsync_WhenDueRecently_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddHours(hours: -1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetOverdueAsync(before: now.AddDays(days: -1));

		await Assert.That(value: result).IsEmpty().Because(message: """
			An operation that just came due is about to be executed by this very run. Escalating it would
			raise an alert for every payment the moment it fell due.
		""");
	}

	[Test]
	public async Task GetOverdueAsync_WhenAlreadyEscalatedForThisOccurrence_ShouldNotReturnAgain()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;
		DateTimeOffset dueAt = now.AddDays(days: -2);

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: dueAt,
			lastMissedAt: dueAt.AddHours(hours: 1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetOverdueAsync(before: now.AddDays(days: -1));

		await Assert.That(value: result).IsEmpty().Because(message: """
			Replaces the old "already marked missed this month" rule. The mark is compared against the due
			instant rather than a calendar boundary: it is current as long as the schedule has not moved
			past it, which is what stops one outage producing an alert on every subsequent run.
		""");
	}

	[Test]
	public async Task GetOverdueAsync_WhenTheMarkBelongsToAnEarlierOccurrence_ShouldReturnAgain()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddDays(days: -2),
			lastMissedAt: now.AddMonths(months: -1)
		);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetOverdueAsync(before: now.AddDays(days: -1));

		await Assert.That(value: result.Count).IsEqualTo(expected: 1).Because(message: """
			A mark left by last month's outage must not silence this month's. Comparing it to the current
			due instant is what distinguishes the two; comparing it to "some time recently" would not.
		""");
	}

	[Test]
	public async Task GetOverdueAsync_WhenInactive_ShouldNotReturn()
	{
		Guid userId = await _userBuilder.CreateAsync();
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
		DateTimeOffset now = DateTimeOffset.UtcNow;

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId, accountId: accountId, categoryId: categoryId,
			nextDueAtUtc: now.AddDays(days: -2)
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id, expectedVersion: 0);

		IReadOnlyList<RecurringTransactionReadModel> result = await _readRepository.GetOverdueAsync(before: now.AddDays(days: -1));

		await Assert.That(value: result).IsEmpty();
	}
}
