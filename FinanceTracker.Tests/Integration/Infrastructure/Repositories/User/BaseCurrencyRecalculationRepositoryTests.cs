using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class BaseCurrencyRecalculationRepositoryTests : DatabaseFixture
{
	private const int MaxAttempts = 3;

	private static readonly Core.ValueObjects.Currency Usd = Core.ValueObjects.Currency.Reconstitute(value: "USD");
	private static readonly Core.ValueObjects.Currency Eur = Core.ValueObjects.Currency.Reconstitute(value: "EUR");
	private static readonly TimeSpan Lease = TimeSpan.FromMinutes(value: 5);

	private BaseCurrencyRecalculationWriteRepository _writeRepository = null!;
	private BaseCurrencyRecalculationReadRepository _readRepository = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = new BaseCurrencyRecalculationWriteRepository(context: Context);
		_readRepository = new BaseCurrencyRecalculationReadRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	private async Task<Guid> RequestForNewUserAsync(Core.ValueObjects.Currency target)
	{
		Guid userId = await _userBuilder.CreateAsync();
		await _writeRepository.RequestAsync(
			userId: userId,
			targetCurrency: target,
			requestedAt: Now,
			ct: CancellationToken.None
		);
		return userId;
	}

	[Test]
	public async Task RequestAsync_ShouldMakeTotalsUnavailable()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);

		await Assert.That(value: await _readRepository.TotalsAreUnavailableAsync(userId: userId)).IsTrue().Because(message: """
			Between the currency changing and the rebuild finishing, the stored totals are still
			denominated in the previous currency. Serving them would not be slightly out of date, it
			would be numbers off by an exchange rate with nothing marking them as wrong.
		""");
	}

	[Test]
	public async Task TotalsAreUnavailableAsync_ForAUserWhoNeverChangedCurrency_ShouldBeFalse()
	{
		Guid userId = await _userBuilder.CreateAsync();

		await Assert.That(value: await _readRepository.TotalsAreUnavailableAsync(userId: userId)).IsFalse()
			.Because(message: "No row means no currency change ever happened. That is the common case and must not withhold totals.");
	}

	[Test]
	public async Task RequestAsync_CalledAgain_ShouldSupersedeTheEarlierTarget()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);
		await _writeRepository.FailAttemptAsync(
			userId: userId,
			error: "boom",
			maxAttempts: MaxAttempts,
			ct: CancellationToken.None
		);

		await _writeRepository.RequestAsync(
			userId: userId,
			targetCurrency: Eur,
			requestedAt: Now,
			ct: CancellationToken.None
		);

		IReadOnlyList<BaseCurrencyRecalculation> claimed = await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		BaseCurrencyRecalculation row = claimed.Single(predicate: c => c.UserId == userId);

		await Assert.That(value: row.TargetCurrency).IsEqualTo(expected: Eur).Because(message: """
			A second change replaces the first outright rather than queueing behind it. Rebuilding into
			USD once the user has moved to EUR is work spent reaching an answer nobody asked for.
		""");
		await Assert.That(value: row.Attempts).IsEqualTo(expected: 0)
			.Because(message: "Attempts counted against the old target say nothing about the new one, and carrying them over would abandon the rebuild early.");
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldNotHandOutARowTwiceWhileTheLeaseHolds()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);

		await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		IReadOnlyList<BaseCurrencyRecalculation> second = await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now.AddMinutes(minutes: 1),
			ct: CancellationToken.None
		);

		await Assert.That(value: second.Any(predicate: c => c.UserId == userId)).IsFalse()
			.Because(message: "Two workers rebuilding the same user's totals at once would write over each other for no gain.");
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldReclaimARowWhoseLeaseExpired()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);

		await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		IReadOnlyList<BaseCurrencyRecalculation> afterExpiry = await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now.Add(timeSpan: Lease).AddMinutes(minutes: 1),
			ct: CancellationToken.None
		);

		await Assert.That(value: afterExpiry.Any(predicate: c => c.UserId == userId)).IsTrue().Because(message: """
			A worker that died mid-rebuild leaves the row claimed. Without picking it back up once the
			lease expires it would stay in progress forever, and the user's totals would never return.
		""");
	}

	[Test]
	public async Task CompleteAsync_WithTheCurrencyItWasAimingAt_ShouldRestoreTotals()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);
		await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		bool completed = await _writeRepository.CompleteAsync(userId: userId, targetCurrency: Usd, ct: CancellationToken.None);

		await Assert.That(value: completed).IsTrue();
		await Assert.That(value: await _readRepository.TotalsAreUnavailableAsync(userId: userId)).IsFalse();
	}

	[Test]
	public async Task CompleteAsync_AfterTheUserChangedCurrencyAgain_ShouldBeRefused()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);
		await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		await _writeRepository.RequestAsync(
			userId: userId,
			targetCurrency: Eur,
			requestedAt: Now.AddMinutes(minutes: 1),
			ct: CancellationToken.None
		);

		bool completed = await _writeRepository.CompleteAsync(
			userId: userId,
			targetCurrency: Usd,
			ct: CancellationToken.None
		);

		await Assert.That(value: completed).IsFalse().Because(message: """
			The finishing worker computed totals in USD, but the row now aims at EUR. Accepting the
			completion would mark EUR totals as ready while the stored numbers are USD — wrong, and
			marked correct, which is worse than being marked unavailable.
		""");
		await Assert.That(value: await _readRepository.TotalsAreUnavailableAsync(userId: userId)).IsTrue()
			.Because(message: "The EUR rebuild has not run yet, so totals stay withheld until it does.");
	}

	[Test]
	public async Task FailAttemptAsync_BelowTheLimit_ShouldReleaseTheRowForAnotherTry()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);
		await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		await _writeRepository.FailAttemptAsync(
			userId: userId,
			error: "rate lookup failed",
			maxAttempts: MaxAttempts,
			ct: CancellationToken.None
		);

		IReadOnlyList<BaseCurrencyRecalculation> reclaimed = await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);

		BaseCurrencyRecalculation row = reclaimed.Single(predicate: c => c.UserId == userId);

		await Assert.That(value: row.Attempts).IsEqualTo(expected: 1);
		await Assert.That(value: row.LastError).IsEqualTo(expected: "rate lookup failed")
			.Because(message: "Whoever looks at a stuck rebuild needs the reason on the row itself, not only in a log line from days ago.");
	}

	[Test]
	public async Task FailAttemptAsync_AtTheLimit_ShouldStopRetryingAndKeepTotalsWithheld()
	{
		Guid userId = await RequestForNewUserAsync(target: Usd);

		for (int attempt = 0; attempt < MaxAttempts; attempt++)
		{
			await _writeRepository.ClaimPendingBatchAsync(
				batchSize: 10,
				leaseDuration: Lease,
				now: Now,
				ct: CancellationToken.None
			);
			await _writeRepository.FailAttemptAsync(
				userId: userId,
				error: "still failing",
				maxAttempts: MaxAttempts,
				ct: CancellationToken.None
			);
		}

		IReadOnlyList<BaseCurrencyRecalculation> afterGivingUp = await _writeRepository.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now.AddHours(hours: 1),
			ct: CancellationToken.None
		);

		await Assert.That(value: afterGivingUp.Any(predicate: c => c.UserId == userId)).IsFalse().Because(message: """
			A rebuild that has failed every time is not helped by running it again, and retrying forever
			would keep it out of sight. Leaving it failed is what makes it findable.
		""");

		await Assert.That(value: await _readRepository.TotalsAreUnavailableAsync(userId: userId)).IsTrue()
			.Because(message: "Giving up on the rebuild does not make the stale totals correct — they are still in the previous currency.");
	}
}
