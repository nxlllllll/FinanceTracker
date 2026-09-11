using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using UnitResult = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class TransferCancellationTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	private static readonly TimeSpan Window = TimeSpan.FromDays(value: 30);

	[Test]
	[Arguments(TransferStatus.PendingCredit)]
	[Arguments(TransferStatus.Completed)]
	public async Task Cancel_WhileThereIsSomethingToGiveBack_ShouldSucceed(TransferStatus status)
	{
		Transfer transfer = TransferFactory.Reconstitute(status: status);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: Now, maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Cancelled);
	}

	[Test]
	[Arguments(TransferStatus.Compensated)]
	[Arguments(TransferStatus.Failed)]
	[Arguments(TransferStatus.Cancelled)]
	public async Task Cancel_WhenNothingIsLeftToGiveBack_ShouldRefuse(TransferStatus status)
	{
		Transfer transfer = TransferFactory.Reconstitute(status: status);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: Now, maxAge: Window);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>().Because(message: """
			A compensated transfer already handed the money back and a failed one is waiting on a human.
			Cancelling either would raise a second reversal for a movement that was undone once already.
		""");

		await Assert.That(value: transfer.Status).IsEqualTo(expected: status);
	}

	[Test]
	public async Task Cancel_PastTheWindow_ShouldRefuseAndLeaveTheStatusAlone()
	{
		Transfer transfer = TransferFactory.Reconstitute(
			status: TransferStatus.Completed,
			occurredAt: Now.AddDays(days: -31)
		);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: Now, maxAge: Window);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<TransferCancellationWindowExpiredException>();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Completed);
	}

	[Test]
	public async Task Cancel_OnTheLastDayOfTheWindow_ShouldStillSucceed()
	{
		Transfer transfer = TransferFactory.Reconstitute(
			status: TransferStatus.Completed,
			occurredAt: Now.AddDays(days: -30)
		);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: Now, maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue().Because(message: """
			The boundary is inclusive: a transfer made exactly the window ago is the last one that can
			still be undone, and an off-by-one here silently shortens the window by a day.
		""");
	}

	[Test]
	public async Task Cancel_WhenTheRateWasStillPending_ShouldCancelIt()
	{
		Transfer transfer = TransferFactory.Reconstitute(
			status: TransferStatus.Completed,
			rateStatus: RateStatus.Pending
		);

		DateTimeOffset cancelledAt = Now.AddHours(hours: 1);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: cancelledAt, maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Cancelled).Because(message: """
			A pending rate is a promise that BalanceAdjustmentJob will come back and correct this balance.
			Leaving it open on a cancelled transfer sends the job after a movement that no longer exists,
			and it ends up escalated as RateAdjustmentFailed.
		""");

		await Assert.That(value: transfer.RateStatusChangedAt).IsEqualTo(expected: cancelledAt);
	}

	[Test]
	public async Task Cancel_WhenTheRateWasAlreadySettled_ShouldLeaveItAlone()
	{
		Transfer transfer = TransferFactory.Reconstitute(
			status: TransferStatus.Completed,
			rateStatus: RateStatus.Exact
		);

		Result<UnitResult, DomainException> result = transfer.Cancel(cancelledAt: Now.AddHours(hours: 1), maxAge: Window);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Exact);
	}

	[Test]
	public async Task Cancel_Twice_ShouldRefuseTheSecondTime()
	{
		Transfer transfer = TransferFactory.Reconstitute(status: TransferStatus.Completed);

		await Assert.That(value: transfer.Cancel(cancelledAt: Now, maxAge: Window).IsSuccess).IsTrue();

		Result<UnitResult, DomainException> second = transfer.Cancel(cancelledAt: Now, maxAge: Window);

		await Assert.That(value: second.IsFailure).IsTrue().Because(message: """
			Cancelling is not idempotent the way acknowledging is: a second pass would raise a second pair
			of reversal events and move the money twice.
		""");
	}
}
