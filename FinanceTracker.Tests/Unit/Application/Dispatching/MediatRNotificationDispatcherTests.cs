using FinanceTracker.Application.Dispatching;
using FinanceTracker.Application.UseCases.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
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
    public async Task DispatchAsync_WithAccountNotification_ShouldPublishAccountEventsNotification()
    {
        Guid accountId = Guid.NewGuid();
        AccountAppNotification notification = new AccountAppNotification(
            AccountId: accountId,
            Events: [new AccountDebited(
                Id: Guid.NewGuid(),
                AccountId: accountId,
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
            notification: Arg.Is<AccountEventsNotification>(n =>
                n.AccountId == accountId &&
                n.Events.Count == 1
            ),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task DispatchAsync_WithNonConvertibleData_ShouldThrowUnknownAggregateTypeException()
    {
        IAppNotification notification = Substitute.For<IAppNotification>();
        notification.Data.Returns(Substitute.For<INotificationData>());

        await Assert.That(
            action: async () => await _dispatcher.DispatchAsync(notification: notification)
        ).Throws<UnknownAggregateTypeException>();
    }
}