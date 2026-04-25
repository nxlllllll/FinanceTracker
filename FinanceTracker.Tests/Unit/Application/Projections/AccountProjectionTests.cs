using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Accounts.Projections;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories.Account;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Projections;

public sealed class AccountProjectionTests
{
	private IAccountWriteRepository _accountWriteRepository = null!;
	private AccountProjection _projection = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();
		_projection = new AccountProjection(accountWriteRepository: _accountWriteRepository);
	}
    
    [Test]
    public async Task Handle_WhenAccountCreated_ShouldCallCreateAsync()
    {
        AccountCreated @event = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Name: "Карта Сбер",
            Type: AccountType.Checking,
            Currency: "RUB",
            Balance: 0,
            OccurredAt: DateTime.UtcNow
        );

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            @event: Arg.Is<AccountCreated>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountRenamed_ShouldCallRenameAsync()
    {
        AccountRenamed @event = new AccountRenamed(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            NewName: "Карта Тинькофф",
            OccurredAt: DateTime.UtcNow
        );

        await _projection.Handle(
            notification: new AccountEventsNotification(AccountId: @event.AccountId, Events: [@event]),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
            @event: Arg.Is<AccountRenamed>(e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountArchived_ShouldCallArchiveAsync()
    {
        AccountArchived @event = new AccountArchived(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow
        );

        await _projection.Handle(
            notification: new AccountEventsNotification(AccountId: @event.AccountId, Events: [@event]),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
            @event: Arg.Is<AccountArchived>(e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountUnarchived_ShouldCallUnarchiveAsync()
    {
        AccountUnarchived @event = new AccountUnarchived(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow
        );

        await _projection.Handle(
            notification: new AccountEventsNotification(AccountId: @event.AccountId, Events: [@event]),
            ct: CancellationToken.None
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
            @event: Arg.Is<AccountUnarchived>(e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenAccountDebited_ShouldCallDebitAsync()
    {
        AccountDebited @event = new AccountDebited(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            ExchangeRate: 1m,
            Description: "Обед",
            OccurredAt: DateTime.UtcNow
        );

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).DebitAsync(
            @event: Arg.Is<AccountDebited>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenAccountCredited_ShouldCallCreditAsync()
    {
        AccountCredited @event = new AccountCredited(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 500m,
            ExchangeRate: 1m,
            Description: "Зарплата",
            OccurredAt: DateTime.UtcNow
        );

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).CreditAsync(
            @event: Arg.Is<AccountCredited>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenAccountBalanceAdjusted_ShouldCallAdjustBalanceAsync()
    {
        AccountBalanceAdjusted @event = new AccountBalanceAdjusted(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            SourceId: Guid.NewGuid(),
            SourceType: "Transaction",
            OldRate: 85m,
            NewRate: 90m,
            Amount: 1000m,
            Delta: 5000m,
            OccurredAt: DateTime.UtcNow
        );

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).AdjustBalanceAsync(
            @event: Arg.Is<AccountBalanceAdjusted>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task Handle_WhenAccountTransferDebited_ShouldCallTransferDebitAsync()
    {
        AccountTransferDebited @event = new AccountTransferDebited(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TransferId: Guid.NewGuid(),
            ToAccountId: Guid.NewGuid(),
            Amount: 5000m,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );
 
        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );
 
        await _projection.Handle(notification: notification, ct: CancellationToken.None);
 
        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).TransferDebitAsync(
            @event: Arg.Is<AccountTransferDebited>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }
 
    [Test]
    public async Task Handle_WhenAccountTransferCredited_ShouldCallTransferCreditAsync()
    {
        AccountTransferCredited @event = new AccountTransferCredited(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            TransferId: Guid.NewGuid(),
            FromAccountId: Guid.NewGuid(),
            Amount: 5000m,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );
 
        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );
 
        await _projection.Handle(notification: notification, ct: CancellationToken.None);
 
        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).TransferCreditAsync(
            @event: Arg.Is<AccountTransferCredited>(predicate: e => e.AccountId == @event.AccountId),
            ct: Arg.Any<CancellationToken>()
        );
    }
}