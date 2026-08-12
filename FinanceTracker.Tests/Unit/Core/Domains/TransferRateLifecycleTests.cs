using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Rate;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class TransferRateLifecycleTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;
	private static DateTimeOffset Later => Now.AddDays(days: 1);

	[Test]
	public async Task Compensate_WhenRateWasPending_ShouldCancelTheRate()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Compensate(occurredAt: Later);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Compensated);
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Cancelled);
		await Assert.That(value: transfer.RateStatus.IsOpen()).IsFalse();
		await Assert.That(value: transfer.RateStatusChangedAt).IsEqualTo(expected: Later);
	}

	[Test]
	public async Task Fail_WhenRateWasPending_ShouldCancelTheRate()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Fail(occurredAt: Later);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Failed);
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Cancelled);
	}

	[Test]
	public async Task Compensate_WhenRateWasAlreadySettled_ShouldSucceedAndLeaveItAlone()
	{
		Transfer transfer = TransferFactory.Create(rateStatus: RateStatus.Exact);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Compensate(occurredAt: Later);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Compensated);
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Exact);
		await Assert.That(value: transfer.RateStatusChangedAt).IsEqualTo(expected: Now);
	}

	[Test]
	public async Task ResolveRate_AfterCompensation_ShouldBeRejected()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		transfer.Compensate(occurredAt: Later);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.ResolveRate(newRate: 95m, changedAt: Later);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidRateStatusTransitionException>();
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m);
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task ResolveRate_ShouldRecomputeAmountTo_UsingTheSharedRounding()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.ResolveRate(newRate: 92.4567m, changedAt: Later);

		decimal expected = Money.ConvertedAmount(amount: 100m, rate: 92.4567m);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 92.4567m);
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: expected);
		await Assert.That(value: transfer.AmountTo.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Resolved);
		await Assert.That(value: transfer.AmountFrom.Amount).IsEqualTo(expected: 100m)
			.Because(message: "The debited side is rate-independent and must never move.");
	}

	[Test]
	public async Task ResolveRate_WhenRecomputedAmountRoundsToZero_ShouldBeRejectedBeforeTheDatabaseSeesIt()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.ResolveRate(newRate: 0.00000001m, changedAt: Later);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Pending)
			.Because(message: "A rejected transition must leave the aggregate untouched — the caller decides what to do next.");
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m)
			.Because(message: "Nothing about the old, valid rate should change on a rejected transition.");
	}

	[Test]
	public async Task ApproximateRate_ShouldCloseTheLifecycle_AndKeepThePlaceholderRate()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 100m,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: 90m,
			rateStatus: RateStatus.Pending
		);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.ApproximateRate(changedAt: Later);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Approximated);
		await Assert.That(value: transfer.RateStatus.IsOpen()).IsFalse();
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m)
			.Because(message: "The placeholder is now the answer, not a guess — it must not be rewritten.");
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 9000m);
	}

	[Test]
	public async Task Create_InATerminalState_ShouldBeRejected()
	{
		Result<Transfer, DomainException> result = Transfer.Create(
			createdAt: Now,
			userId: Guid.CreateVersion7(),
			fromAccountId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 100m,
			currencyFrom: Currency.Reconstitute(value: "USD"),
			currencyTo: Currency.Reconstitute(value: "RUB"),
			exchangeRate: 90m,
			rateStatus: RateStatus.Resolved,
			description: null,
			occurredAt: Now
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidRateStatusTransitionException>()
			.Because(message: "Resolved means 'the job corrected the balance'. Being born there would be a lie, and a back door around the state machine.");
	}
}
