using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.RecurringTransaction;
using FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.RecurringTransaction;

public sealed class RecurringTransactionRescheduleTests : DatabaseFixture
{
	private static readonly TimeZoneId Auckland = TimeZoneId.Create(value: "Pacific/Auckland").Value;
	private static readonly TimeZoneId Honolulu = TimeZoneId.Create(value: "Pacific/Honolulu").Value;

	private RecurringTransactionWriteRepository _writeRepository = null!;
	private UserBuilder _userBuilder = null!;
	private AccountBuilder _accountBuilder = null!;
	private CategoryBuilder _categoryBuilder = null!;
	private RecurringTransactionBuilder _recurringTransactionBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new RecurringTransactionWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
		_userBuilder = new UserBuilder(context: Context);
		_accountBuilder = new AccountBuilder(context: Context);
		_categoryBuilder = new CategoryBuilder(context: Context);
		_recurringTransactionBuilder = new RecurringTransactionBuilder(context: Context);
	}

	private async Task<RecurringTransactionEntity> ReadAsync(Guid id)
		=> await Context.RecurringTransactions.AsNoTracking().FirstAsync(predicate: r => r.Id == id);

	[Test]
	public async Task CreateAsync_ShouldStoreTheDueInstantTheAggregateComputed()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		Core.Domains.RecurringTransaction.RecurringTransaction operation = RecurringTransactionFactory.Create(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			timeZone: Auckland
		).Value!;

		await _writeRepository.CreateAsync(recurringTransaction: operation);
		await Context.SaveChangesAsync();

		RecurringTransactionEntity stored = await ReadAsync(id: operation.Id);

		await Assert.That(value: stored.NextDueAtUtc).IsEqualTo(expected: operation.NextDueAtUtc).Because(message: $"""
			The aggregate scheduled this for {operation.NextDueAtUtc:u}, which is midnight on the 1st in
			Auckland. A repository that recomputed it in UTC would store {RecurringDueDate.Next(dayOfMonth: 1, timeZone: TimeZoneId.Utc, after: operation.CreatedAt):u}
			instead — the same day on a calendar the user never chose.
		""");
	}

	[Test]
	public async Task Reschedule_MovingWestward_ShouldPushTheDueInstantForwardWithoutSkippingAMonth()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset aucklandDue = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			nextDueAtUtc: aucklandDue
		);

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);

		RecurringTransactionEntity stored = await ReadAsync(id: id);

		DateTimeOffset expected = new DateTimeOffset(year: 2026, month: 9, day: 1, hour: 10, minute: 0, second: 0, offset: TimeSpan.Zero);

		await Assert.That(value: stored.NextDueAtUtc).IsEqualTo(expected: expected).Because(message: """
			Honolulu's 1 September begins at 10:00 UTC, twenty-two hours after Auckland's. The occurrence
			is the same one — the user still pays on the 1st — but it now falls where their day does.
		""");
	}

	[Test]
	public async Task Reschedule_WhenAnOperationIsOverdue_ShouldLeaveItOverdue()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset overdue = DateTimeOffset.UtcNow.AddDays(days: -3);

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: overdue.Day,
			nextDueAtUtc: overdue
		);

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);

		RecurringTransactionEntity stored = await ReadAsync(id: id);

		await Assert.That(value: stored.NextDueAtUtc < DateTimeOffset.UtcNow).IsTrue().Because(message: $"""
			It was due {overdue:u} and is now scheduled for {stored.NextDueAtUtc:u}. Anything in the future
			would mean the move quietly deferred a payment the user was already owed — a month of silence
			where they expected a charge.
		""");
	}

	[Test]
	public async Task Reschedule_ShouldLeaveDeactivatedOperationsAlone()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset aucklandDue = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			nextDueAtUtc: aucklandDue
		);

		await _writeRepository.DeactivateAsync(recurringTransactionId: id, expectedVersion: 0);

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);

		RecurringTransactionEntity stored = await ReadAsync(id: id);

		await Assert.That(value: stored.NextDueAtUtc).IsEqualTo(expected: aucklandDue).Because(message: """
			A deactivated template fires for nobody, so there is nothing to reschedule. Touching it would
			also mean its stored instant no longer matches the zone it was last computed in, and
			reactivating would inherit a schedule nobody asked for.
		""");
	}

	[Test]
	public async Task Reschedule_ShouldLeaveAnotherUsersOperationsAlone()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid otherUserId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: otherUserId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: otherUserId);

		DateTimeOffset aucklandDue = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid otherId = await _recurringTransactionBuilder.CreateAsync(
			userId: otherUserId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			nextDueAtUtc: aucklandDue
		);

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);

		RecurringTransactionEntity stored = await ReadAsync(id: otherId);

		await Assert.That(value: stored.NextDueAtUtc).IsEqualTo(expected: aucklandDue).Because(message: """
			One person moving cannot move anyone else's schedules. The unnest statement matches on id
			alone, so the filtering happens when the batch is assembled — which makes this worth asserting
			rather than assuming.
		""");
	}

	[Test]
	public async Task Reschedule_WhenTheInstantDoesNotMove_ShouldNotTouchTheRowVersion()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset aucklandDue = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			nextDueAtUtc: aucklandDue
		);

		int versionBefore = (await ReadAsync(id: id)).RowVersion;

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Auckland);

		RecurringTransactionEntity stored = await ReadAsync(id: id);

		await Assert.That(value: stored.NextDueAtUtc).IsEqualTo(expected: aucklandDue);
		await Assert.That(value: stored.RowVersion).IsEqualTo(expected: versionBefore);
	}

	[Test]
	public async Task Reschedule_WhenTheInstantMoves_ShouldBumpTheRowVersion()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);
		Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
		Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

		DateTimeOffset aucklandDue = new DateTimeOffset(year: 2026, month: 8, day: 31, hour: 12, minute: 0, second: 0, offset: TimeSpan.Zero);

		Guid id = await _recurringTransactionBuilder.CreateAsync(
			userId: userId,
			accountId: accountId,
			categoryId: categoryId,
			dayOfMonth: 1,
			nextDueAtUtc: aucklandDue
		);

		int versionBefore = (await ReadAsync(id: id)).RowVersion;

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);

		RecurringTransactionEntity stored = await ReadAsync(id: id);

		await Assert.That(value: stored.RowVersion).IsEqualTo(expected: versionBefore + 1);
	}

	[Test]
	public async Task Reschedule_WhenTheUserHasNoActiveOperations_ShouldDoNothingWithoutFailing()
	{
		Guid userId = await _userBuilder.CreateAsync(timeZone: Auckland);

		await _writeRepository.RescheduleAllForUserAsync(userId: userId, timeZone: Honolulu);
	}
}
