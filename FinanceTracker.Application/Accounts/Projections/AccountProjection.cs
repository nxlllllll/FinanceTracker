using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using MediatR;

namespace FinanceTracker.Application.Accounts.Projections;

public sealed class AccountProjection(
	IAccountWriteRepository accountWriteRepository
) : INotificationHandler<AccountEventsNotification>
{
	public async Task Handle(
		AccountEventsNotification notification,
		CancellationToken ct = default)
	{
		foreach (IEvent @event in notification.Events)
			await HandleAsync(@event: @event, ct: ct);
	}

	private async Task HandleAsync(IEvent @event, CancellationToken ct)
	{
		switch (@event)
		{
			case AccountCreated e: await HandleAsync(e, ct); break;
			case AccountRenamed e: await HandleAsync(e, ct); break;
			case AccountArchived e: await HandleAsync(e, ct); break;
			case AccountUnarchived e: await HandleAsync(e, ct); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	private async Task HandleAsync(AccountCreated @event, CancellationToken ct)
		=> await accountWriteRepository.CreateAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountRenamed @event, CancellationToken ct)
		=> await accountWriteRepository.RenameAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountArchived @event, CancellationToken ct)
		=> await accountWriteRepository.ArchiveAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountUnarchived @event, CancellationToken ct)
		=> await accountWriteRepository.UnarchiveAsync(@event: @event, ct: ct);
}