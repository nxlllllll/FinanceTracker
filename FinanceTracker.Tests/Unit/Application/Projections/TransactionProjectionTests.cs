using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Application.Transactions.Projections;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Domains.Transactions.Events;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Projections;

public sealed class TransactionProjectionTests
{
	private ITransactionWriteRepository _transactionWriteRepository;
	private IAccountWriteRepository _accountWriteRepository;
	private TransactionProjection _projection;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_accountWriteRepository = Substitute.For<IAccountWriteRepository>();
		_projection = new TransactionProjection(
			transactionWriteRepository: _transactionWriteRepository,
			accountWriteRepository: _accountWriteRepository
		);
	}
	
	[Test]
    public async Task Handle_WhenTransactionCreated_ShouldCallCreateAndUpdateBalance()
    {
        TransactionCreated @event = new TransactionCreated(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Debit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
            @event: Arg.Is<TransactionCreated>(
                predicate: e => e.TransactionId == @event.TransactionId
            ), ct: Arg.Any<CancellationToken>()
        );

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UpdateBalanceAsync(
            accountId: @event.AccountId,
            amount: -1000m,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionCreatedWithCredit_ShouldUpdateBalancePositively()
    {
        TransactionCreated @event = new TransactionCreated(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 1000m,
            Direction: DirectionType.Credit,
            ExchangeRate: 1m,
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UpdateBalanceAsync(
            accountId: @event.AccountId,
            amount: 1000m,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionCreatedWithExchangeRate_ShouldApplyExchangeRate()
    {
        TransactionCreated @event = new TransactionCreated(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            Amount: 100m,
            Direction: DirectionType.Debit,
            ExchangeRate: 90m, // 100 USD * 90 = 9000 RUB
            Description: null,
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UpdateBalanceAsync(
            accountId: @event.AccountId,
            amount: -9000m,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionCategoryChanged_ShouldCallChangeCategoryAsync()
    {
        TransactionCategoryChanged @event = new TransactionCategoryChanged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            CategoryId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeCategoryAsync(
            @event: Arg.Is<TransactionCategoryChanged>(
                predicate: e => e.TransactionId == @event.TransactionId
            ), ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionDescriptionChanged_ShouldCallChangeDescriptionAsync()
    {
        TransactionDescriptionChanged @event = new TransactionDescriptionChanged(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            Description: "Новая заметка",
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ChangeDescriptionAsync(
            @event: Arg.Is<TransactionDescriptionChanged>(
                predicate: e => e.TransactionId == @event.TransactionId
            ), ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionIncluded_ShouldCallIncludeAsync()
    {
        TransactionIncluded @event = new TransactionIncluded(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).IncludeAsync(
            @event: Arg.Is<TransactionIncluded>(
                predicate: e => e.TransactionId == @event.TransactionId
            ), ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenTransactionExcluded_ShouldCallExcludeAsync()
    {
        TransactionExcluded @event = new TransactionExcluded(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            OccurredAt: DateTime.UtcNow
        );

        TransactionEventsNotification notification = new TransactionEventsNotification(
            TransactionId: @event.TransactionId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).ExcludeAsync(
            @event: Arg.Is<TransactionExcluded>(
                predicate: e => e.TransactionId == @event.TransactionId
            ), ct: Arg.Any<CancellationToken>()
        );
    }
}