using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Dispatching;

public sealed class MediatRNotificationDispatcherTests
{
    private IPublisher _publisher = null!;
    private MediatRNotificationDispatcher _dispatcher = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _publisher = Substitute.For<IPublisher>();
        _dispatcher = new MediatRNotificationDispatcher(publisher: _publisher);
    }

    [Test]
    public async Task DispatchAsync_WithAccountAggregateType_ShouldPublishAccountEventsNotification()
    {
        AggregateNotification notification = new AggregateNotification(
            AggregateId: Guid.NewGuid(),
            AggregateType: nameof(Account),
            Events: [new AccountDebited(
                Id: Guid.NewGuid(),
                AccountId: Guid.NewGuid(),
                TransactionId: Guid.NewGuid(),
                CategoryId: Guid.NewGuid(),
                Amount: 1000m,
                ExchangeRate: 1m,
                Description: null,
                OccurredAt: DateTime.UtcNow
            )]
        );

        await _dispatcher.DispatchAsync(notification: notification);

        await _publisher.Received(requiredNumberOfCalls: 1).Publish(
            notification: Arg.Is<AccountEventsNotification>(predicate: n =>
                n.AccountId == notification.AggregateId &&
                n.Events.Count == 1
            ),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task DispatchAsync_WithUnknownAggregateType_ShouldThrowUnknownAggregateTypeException()
    {
        AggregateNotification notification = new AggregateNotification(
            AggregateId: Guid.NewGuid(),
            AggregateType: "Unknown",
            Events: []
        );

        await Assert.That(
            action: async () => await _dispatcher.DispatchAsync(notification: notification)
        ).Throws<UnknownAggregateTypeException>();
    }
}