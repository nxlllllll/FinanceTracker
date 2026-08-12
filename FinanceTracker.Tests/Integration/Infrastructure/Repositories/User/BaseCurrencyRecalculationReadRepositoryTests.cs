using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Repositories.User;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.User;

public sealed class BaseCurrencyRecalculationReadRepositoryTests : DatabaseFixture
{
	private const int MaxAttempts = 2;

	private static readonly Core.ValueObjects.Currency Usd = Core.ValueObjects.Currency.Reconstitute(value: "USD");
	private static readonly TimeSpan Lease = TimeSpan.FromMinutes(value: 5);

	private BaseCurrencyRecalculationWriteRepository _writer = null!;
	private BaseCurrencyRecalculationReadRepository _reader = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writer = new BaseCurrencyRecalculationWriteRepository(context: Context);
		_reader = new BaseCurrencyRecalculationReadRepository(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	private async Task<Guid> PendingAsync()
	{
		Guid userId = await _userBuilder.CreateAsync();
		await _writer.RequestAsync(
			userId: userId,
			targetCurrency: Usd,
			requestedAt: Now,
			ct: CancellationToken.None
		);
		return userId;
	}

	private async Task<Guid> InProgressAsync()
	{
		Guid userId = await PendingAsync();
		await _writer.ClaimPendingBatchAsync(
			batchSize: 10,
			leaseDuration: Lease,
			now: Now,
			ct: CancellationToken.None
		);
		return userId;
	}

	private async Task<Guid> CompletedAsync()
	{
		Guid userId = await InProgressAsync();
		await _writer.CompleteAsync(
			userId: userId,
			targetCurrency: Usd,
			ct: CancellationToken.None
		);
		return userId;
	}

	private async Task<Guid> FailedAsync()
	{
		Guid userId = await PendingAsync();

		for (int attempt = 0; attempt < MaxAttempts; attempt++)
		{
			await _writer.ClaimPendingBatchAsync(
				batchSize: 10,
				leaseDuration: Lease,
				now: Now,
				ct: CancellationToken.None
			);
			await _writer.FailAttemptAsync(
				userId: userId,
				error: "still failing",
				maxAttempts: MaxAttempts,
				ct: CancellationToken.None
			);
		}

		return userId;
	}

	[Test]
	public async Task WithNoRow_ShouldAllowTotals()
	{
		Guid userId = await _userBuilder.CreateAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: userId)).IsFalse().Because(message: """
			Most users never change base currency and so never get a row. If the absence of one withheld
			totals, the feature would blank the main screen for everybody.
		""");
	}

	[Test]
	public async Task WhilePending_ShouldWithholdTotals()
	{
		Guid userId = await PendingAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: userId)).IsTrue()
			.Because(message: "The currency has changed but nothing has been recomputed, so the stored amounts are in the previous one.");
	}

	[Test]
	public async Task WhileInProgress_ShouldWithholdTotals()
	{
		Guid userId = await InProgressAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: userId)).IsTrue().Because(message: """
			A rebuild in flight has rewritten some periods and not others, so the set is a mix of two
			currencies — readable, plausible, and adding up to nothing real.
		""");
	}

	[Test]
	public async Task OnceCompleted_ShouldAllowTotals()
	{
		Guid userId = await CompletedAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: userId)).IsFalse()
			.Because(message: "Totals now match the current base currency, which is the whole point of the rebuild.");
	}

	[Test]
	public async Task OnceFailed_ShouldStillWithholdTotals()
	{
		Guid userId = await FailedAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: userId)).IsTrue().Because(message: """
			Giving up on the rebuild does not make the stored amounts correct — they are still in the
			previous currency. Treating exhausted retries as success would turn an operational problem
			into wrong numbers presented as right.
		""");
	}

	[Test]
	public async Task ShouldAnswerPerUser()
	{
		Guid pendingUser = await PendingAsync();
		Guid untouchedUser = await _userBuilder.CreateAsync();

		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: pendingUser)).IsTrue();
		await Assert.That(value: await _reader.TotalsAreUnavailableAsync(userId: untouchedUser)).IsFalse()
			.Because(message: "One user's rebuild must not blank another user's screen.");
	}
}
