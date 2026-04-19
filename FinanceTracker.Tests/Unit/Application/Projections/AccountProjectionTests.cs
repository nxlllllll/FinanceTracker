using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Projections;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Repositories;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application;

public sealed class AccountProjectionTests
{
    private IAccountWriteRepository _accountWriteRepository;
    private AccountProjection _projection;

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
            AccountType: "checking",
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

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).RenameAsync(
            @event: Arg.Is<AccountRenamed>(predicate: e => e.AccountId == @event.AccountId),
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

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).ArchiveAsync(
            @event: Arg.Is<AccountArchived>(predicate: e => e.AccountId == @event.AccountId), 
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

        AccountEventsNotification notification = new AccountEventsNotification(
            AccountId: @event.AccountId,
            Events: [@event]
        );

        await _projection.Handle(notification: notification, ct: CancellationToken.None);

        await _accountWriteRepository.Received(requiredNumberOfCalls: 1).UnarchiveAsync(
            @event: Arg.Is<AccountUnarchived>(predicate: e => e.AccountId == @event.AccountId), 
            ct: Arg.Any<CancellationToken>()
        );
    }
}