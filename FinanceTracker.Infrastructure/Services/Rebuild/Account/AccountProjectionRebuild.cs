using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Services.Rebuild;

namespace FinanceTracker.Infrastructure.Services.Rebuild.Account;

[Projection(name: "account", aggregateType: AggregateTypeNames.Account)]
public sealed class AccountProjectionRebuild(
	IAccountWriteRepository repository,
	AccountDomainEventApplier applier
) : IProjectionRebuild
{
	public Task ClearAsync(Guid aggregateId, CancellationToken ct = default)
		=> repository.DeleteAsync(accountId: aggregateId, ct: ct);

	public Task ApplyAsync(IEvent @event, CancellationToken ct = default)
		=> applier.ApplyAsync(@event: @event, ct: ct);
}
