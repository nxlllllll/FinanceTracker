using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class TransactionCancellationTests
{
	private static readonly DateTimeOffset RecordedAt = FakeDateProvider.Default.UtcNow;
	private static readonly TimeSpan Window = TimeSpan.FromDays(value: 30);

	private static Transaction Cancelled(RateStatus rateStatus = RateStatus.Pending, bool isExcluded = false)
	{
		Transaction transaction = TransactionFactory.Create(rateStatus: rateStatus, isExcluded: isExcluded, createdAt: RecordedAt);
		transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);
		return transaction;
	}

	[Test]
	public async Task Create_ShouldLeaveTheTransactionStanding()
	{
		Transaction transaction = TransactionFactory.Create();

		await Assert.That(value: transaction.IsCancelled).IsFalse();
		await Assert.That(value: transaction.CancelledAt).IsNull();
	}

	[Test]
	public async Task Cancel_WithinTheWindow_ShouldMarkTheTransactionCancelled()
	{
		Transaction transaction = TransactionFactory.Create(createdAt: RecordedAt);

		Result<UnitResult, DomainException> result = transaction.Cancel(cancelledAt: RecordedAt.AddDays(days: 5), maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transaction.IsCancelled).IsTrue();
		await Assert.That(value: transaction.CancelledAt).IsEqualTo(expected: RecordedAt.AddDays(days: 5));
	}

	[Test]
	public async Task Cancel_OnTheFinalDayOfTheWindow_ShouldSucceed()
	{
		Transaction transaction = TransactionFactory.Create(createdAt: RecordedAt);

		Result<UnitResult, DomainException> result = transaction.Cancel(cancelledAt: RecordedAt.Add(timeSpan: Window), maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "The window is inclusive of its final moment. Pinning the boundary matters because an off-by-one here is invisible in every other test — they all sit comfortably inside or outside the range.");
	}

	[Test]
	public async Task Cancel_PastTheWindow_ShouldFail()
	{
		Transaction transaction = TransactionFactory.Create(createdAt: RecordedAt);

		Result<UnitResult, DomainException> result = transaction.Cancel(
			cancelledAt: RecordedAt.Add(timeSpan: Window).AddTicks(ticks: 1),
			maxAge: Window
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<TransactionCancellationWindowExpiredException>();
		await Assert.That(value: transaction.IsCancelled).IsFalse();
	}

	[Test]
	public async Task Cancel_BackdatedTransaction_ShouldMeasureAgeFromWhenItWasRecorded()
	{
		Transaction transaction = TransactionFactory.Create(
			createdAt: RecordedAt,
			occurredAt: RecordedAt.AddMonths(months: -3)
		);

		Result<UnitResult, DomainException> result = transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "Back-dating is permitted for months, so measuring the window from OccurredAt would leave a transaction un-cancellable at the very moment it was entered. This is the case the whole CreatedAt field exists for.");
	}

	[Test]
	public async Task Cancel_Twice_ShouldFail()
	{
		Transaction transaction = Cancelled();

		Result<UnitResult, DomainException> result = transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: result.IsFailure).IsTrue()
			.Because(message: "Someone cancelling expects money back. Answering success on a repeat without moving any would report a refund that never happened — this is deliberately not the no-op that Exclude returns.");
		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task Cancel_ExcludedTransaction_ShouldSucceed()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true, createdAt: RecordedAt);

		Result<UnitResult, DomainException> result = transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "Exclusion governs analytics, cancellation governs the balance. An excluded transaction still moved money and must remain cancellable — the handler is what skips the category totals for it.");
	}

	[Test]
	public async Task Cancel_WithAPendingRate_ShouldTakeItOutOfTheAdjustmentQueue()
	{
		Transaction transaction = TransactionFactory.Create(rateStatus: RateStatus.Pending, createdAt: RecordedAt);

		transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: transaction.RateStatus).IsEqualTo(expected: RateStatus.Cancelled)
			.Because(message: "BalanceAdjustmentJob selects on RateStatus.IsOpen(). A cancelled transaction left pending would have the rate difference posted to a balance whose movement has already been compensated away.");
		await Assert.That(value: transaction.RateStatus.IsOpen()).IsFalse();
	}

	[Test]
	public async Task Cancel_WithAnExactRate_ShouldLeaveTheRateStatusAlone()
	{
		Transaction transaction = TransactionFactory.Create(rateStatus: RateStatus.Exact, createdAt: RecordedAt);

		transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: transaction.RateStatus).IsEqualTo(expected: RateStatus.Exact);
	}

	[Test]
	public async Task Cancel_WithAnApproximatedRate_ShouldLeaveTheRateStatusAlone()
	{
		Transaction transaction = TransactionFactory.Create(rateStatus: RateStatus.Approximated, createdAt: RecordedAt);

		transaction.Cancel(cancelledAt: RecordedAt, maxAge: Window);

		await Assert.That(value: transaction.RateStatus).IsEqualTo(expected: RateStatus.Approximated)
			.Because(message: "Approximated reads like unfinished business but IsOpen() covers Pending alone, so the job never returns to it. Rewriting it to Cancelled would discard the fact that a real rate was applied.");
	}

	[Test]
	public async Task Exclude_AfterCancellation_ShouldFail()
	{
		Transaction transaction = Cancelled();

		Result<bool, DomainException> result = transaction.Exclude();

		await Assert.That(value: result.IsFailure).IsTrue()
			.Because(message: "Cancelling already subtracted this transaction from the category totals. Excluding it afterwards would subtract the same contribution twice and drive the category below zero.");
		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task Include_AfterCancellation_ShouldFail()
	{
		Transaction transaction = Cancelled(isExcluded: true);

		Result<bool, DomainException> result = transaction.Include();

		await Assert.That(value: result.IsFailure).IsTrue()
			.Because(message: "A cancelled transaction that was excluded never contributed to the totals. Re-including it would add a contribution for money that has already been returned.");
		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task ChangeCategory_AfterCancellation_ShouldFail()
	{
		Transaction transaction = Cancelled();

		Result<bool, DomainException> result = transaction.ChangeCategory(categoryId: Guid.CreateVersion7());

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task ChangeDescription_AfterCancellation_ShouldFail()
	{
		Transaction transaction = Cancelled();

		Result<bool, DomainException> result = transaction.ChangeDescription(description: "попытка изменить");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>();
	}

	[Test]
	public async Task ChangeDescription_AfterCancellationOfAnExcludedTransaction_ShouldReportCancellationNotExclusion()
	{
		Transaction transaction = Cancelled(isExcluded: true);

		Result<bool, DomainException> result = transaction.ChangeDescription(description: "попытка изменить");

		await Assert.That(value: result.Error).IsTypeOf<CancelledOperationException>()
			.Because(message: "Both guards apply here. Cancellation is the terminal state and exclusion only an accounting mode, so reporting the exclusion would send the caller looking for a way to re-include a transaction that is finished either way.");
	}
}
