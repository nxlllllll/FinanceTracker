using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services;

public sealed class AccountDomainEventApplierTests
{
	private sealed record UnknownTestEvent : IEvent
	{
		public Guid Id => Guid.CreateVersion7();
		public DateTimeOffset OccurredAt => FakeDateProvider.Default.UtcNow;
	}
	
	private IAccountWriteRepository _repository = null!;
	private AccountDomainEventApplier _applier = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_repository = Substitute.For<IAccountWriteRepository>();
		_applier = new AccountDomainEventApplier(repository: _repository);
	}

	[Test]
	public async Task ApplyAsync_AccountCreated_ShouldCallCreateAsync()
	{
		AccountCreated @event = new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			UserId: Guid.CreateVersion7(),
			Name: Name.Create(value: "Карта").Value,
			Type: AccountType.Checking,
			Currency: Currency.Reconstitute(value: "RUB"),
			Balance: 1000m,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).CreateAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountDebited_ShouldCallDebitAsync()
	{
		AccountDebited @event = new AccountDebited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).DebitAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountCredited_ShouldCallCreditAsync()
	{
		AccountCredited @event = new AccountCredited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransactionId: Guid.CreateVersion7(),
			CategoryId: Guid.CreateVersion7(),
			Amount: 500m,
			ExchangeRate: 1m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).CreditAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountRenamed_ShouldCallRenameAsync()
	{
		AccountRenamed @event = new AccountRenamed(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			NewName: Name.Create(value: "Новое имя").Value,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).RenameAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountArchived_ShouldCallArchiveAsync()
	{
		AccountArchived @event = new AccountArchived(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountUnarchived_ShouldCallUnarchiveAsync()
	{
		AccountUnarchived @event = new AccountUnarchived(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountTransferDebited_ShouldCallTransferDebitAsync()
	{
		AccountTransferDebited @event = new AccountTransferDebited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransferId: Guid.CreateVersion7(),
			ToAccountId: Guid.CreateVersion7(),
			Amount: 1000m,
			ForexRate: 1m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).TransferDebitAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountTransferCredited_ShouldCallTransferCreditAsync()
	{
		AccountTransferCredited @event = new AccountTransferCredited(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransferId: Guid.CreateVersion7(),
			FromAccountId: Guid.CreateVersion7(),
			Amount: 1000m,
			ExchangeRate: 0.011m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).TransferCreditAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountTransferRefunded_ShouldCallRefundTransferAsync()
	{
		AccountTransferRefunded @event = new AccountTransferRefunded(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			TransferId: Guid.CreateVersion7(),
			Amount: 1000m,
			Description: null,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).RefundTransferAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_AccountBalanceAdjusted_ShouldCallAdjustBalanceAsync()
	{
		AccountBalanceAdjusted @event = new AccountBalanceAdjusted(
			Id: Guid.CreateVersion7(),
			AccountId: Guid.CreateVersion7(),
			SourceId: Guid.CreateVersion7(),
			SourceType: "Transaction",
			OldRate: 85m,
			NewRate: 90m,
			Amount: 1000m,
			Delta: 5000m,
			OccurredAt: FakeDateProvider.Default.UtcNow
		);

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.Received(requiredNumberOfCalls: 1).AdjustBalanceAsync(
			@event: @event,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ApplyAsync_UnknownEvent_ShouldNotCallAnyRepository()
	{
		UnknownTestEvent @event = new UnknownTestEvent();

		await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await _repository.DidNotReceiveWithAnyArgs().CreateAsync(
			@event: Arg.Any<AccountCreated>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}