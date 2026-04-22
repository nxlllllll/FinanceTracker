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
			case AccountCreated e: await HandleAsync(@event: e, ct: ct); break;
			case AccountDebited e: await HandleAsync(@event: e, ct: ct); break;
			case AccountCredited e: await HandleAsync(@event: e, ct: ct); break;
			default: throw new UnknownEventException(message: "Event is unknown.", eventType: @event.GetType());
		}
	}

	private async Task HandleAsync(AccountCreated @event, CancellationToken ct)
		=> await accountWriteRepository.CreateAsync(@event: @event, ct: ct);

	private async Task HandleAsync(AccountDebited @event, CancellationToken ct)
		=> await accountWriteRepository.DebitAsync(@event: @event, ct: ct);
	
	private async Task HandleAsync(AccountCredited @event, CancellationToken ct)
		=> await accountWriteRepository.CreditAsync(@event: @event, ct: ct);
}