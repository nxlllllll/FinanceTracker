using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class TransferTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();
		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = Guid.CreateVersion7();

		Transfer transfer = TransferFactory.Create(
			userId: userId,
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amount: 1000m,
			currencyFrom: "RUB",
			currencyTo: "RUB",
			exchangeRate: 1m,
			isRatePending: false
		);

		await Assert.That(value: transfer.Id).IsNotDefault();
		await Assert.That(value: transfer.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: transfer.FromAccountId).IsEqualTo(expected: fromAccountId);
		await Assert.That(value: transfer.ToAccountId).IsEqualTo(expected: toAccountId);
		await Assert.That(value: transfer.AmountFrom.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 1m);
		await Assert.That(value: transfer.IsRatePending).IsFalse();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.PendingCredit);
		await Assert.That(value: transfer.OccurredAt).IsNotDefault();
		await Assert.That(value: transfer.CreatedAt).IsNotDefault();
	}

	[Test]
	public async Task Create_WhenBackdated_ShouldKeepOccurredAtAndCreatedAtIndependent()
	{
		DateTimeOffset createdAt = FakeDateProvider.Default.UtcNow;
		DateTimeOffset occurredAt = createdAt.AddDays(days: -3);

		Transfer transfer = TransferFactory.Create(occurredAt: occurredAt, createdAt: createdAt);

		await Assert.That(value: transfer.OccurredAt).IsEqualTo(expected: occurredAt);
		await Assert.That(value: transfer.CreatedAt).IsEqualTo(expected: createdAt);
	}

	[Test]
	public async Task Create_WithDifferentCurrencies_ShouldComputeAmountTo()
	{
		Transfer transfer = TransferFactory.Create(
			amount: 1000m,
			currencyFrom: "RUB",
			currencyTo: "USD",
			exchangeRate: 0.011m
		);

		await Assert.That(value: transfer.AmountFrom.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: transfer.AmountFrom.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: 11m);
		await Assert.That(value: transfer.AmountTo.Currency.Value).IsEqualTo(expected: "USD");
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 0.011m);
	}

	[Test]
	public async Task Create_WithZeroExchangeRate_ShouldReturnFailure()
	{
		Result<Transfer, DomainException> result = Transfer.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: Guid.CreateVersion7(),
			fromAccountId: Guid.CreateVersion7(),
			toAccountId: Guid.CreateVersion7(),
			amount: 1000m,
			currencyFrom: Currency.Create(value: "RUB").Value,
			currencyTo: Currency.Create(value: "USD").Value,
			exchangeRate: 0m,
			isRatePending: false,
			description: null,
			occurredAt: FakeDateProvider.Default.UtcNow
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidExchangeRateException>();
	}

	[Test]
	public async Task Create_WithSameAccounts_ShouldReturnFailure()
	{
		Guid accountId = Guid.CreateVersion7();

		Result<Transfer, DomainException> result = Transfer.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: Guid.CreateVersion7(),
			fromAccountId: accountId,
			toAccountId: accountId,
			amount: 1000m,
			currencyFrom: Currency.Create(value: "RUB").Value,
			currencyTo: Currency.Create(value: "RUB").Value,
			exchangeRate: 1m,
			isRatePending: false,
			description: null,
			occurredAt: FakeDateProvider.Default.UtcNow
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<SameAccountTransferException>();
	}

	[Test]
	public async Task Create_WithPendingRate_ShouldSetIsRatePendingTrue()
	{
		Transfer transfer = TransferFactory.Create(isRatePending: true);
		await Assert.That(value: transfer.IsRatePending).IsTrue();
	}

	[Test]
	public async Task Create_WithDescription_ShouldSetDescription()
	{
		Transfer transfer = TransferFactory.Create(description: "перевод");
		await Assert.That(value: transfer.Description).IsEqualTo(expected: "перевод");
	}

	[Test]
	public async Task Create_WithoutDescription_ShouldHaveNullDescription()
	{
		Transfer transfer = TransferFactory.Create(description: null);
		await Assert.That(value: transfer.Description).IsNull();
	}

	[Test]
	public async Task Complete_FromPendingCredit_ShouldSucceed()
	{
		Transfer transfer = TransferFactory.Create();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Complete();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Completed);
	}

	[Test]
	public async Task Complete_FromCompleted_ShouldReturnFailure()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Complete();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Complete();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
	}

	[Test]
	public async Task Complete_FromCompensated_ShouldReturnFailure()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Compensate();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Complete();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
	}

	[Test]
	public async Task Compensate_FromPendingCredit_ShouldSucceed()
	{
		Transfer transfer = TransferFactory.Create();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Compensate();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Compensated);
	}

	[Test]
	public async Task Compensate_FromCompleted_ShouldReturnFailure()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Complete();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Compensate();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
	}

	[Test]
	public async Task Fail_FromPendingCredit_ShouldSucceed()
	{
		Transfer transfer = TransferFactory.Create();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Fail();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Failed);
	}

	[Test]
	public async Task Fail_FromCompleted_ShouldReturnFailure()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Complete();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Fail();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
	}

	[Test]
	public async Task Fail_FromFailed_ShouldReturnFailure()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Fail();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Fail();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransferStatusException>();
	}

	[Test]
	public async Task Fail_FromCompensated_ShouldSucceed()
	{
		Transfer transfer = TransferFactory.Create();
		transfer.Compensate();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transfer.Fail();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Failed);
	}
}
